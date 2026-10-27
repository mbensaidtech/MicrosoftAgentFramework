#!/usr/bin/env bash
# Publishes Dashboard/LabDashboard/labs.json as the reference lab catalog of the admin dashboard
# (PUT /api/catalog, schemaVersion 1). Runs in GitHub Actions and locally.
#
#   publish-lab-catalog.sh [--dry-run] [--file <labs.json>]
#   publish-lab-catalog.sh --check
#
# Environment (not needed by --dry-run):
#   ADMIN_DASHBOARD_URL     e.g. https://labs-admin-dash.vercel.app
#   CATALOG_PUBLISH_TOKEN   the catalog publish token (64 hexadecimal characters)
#
# Never add `set -x`: the token would be printed.
set -euo pipefail

readonly SOURCE_PATH="Dashboard/LabDashboard/labs.json"
readonly MAX_BODY_BYTES=$((256 * 1024))

usage() {
  cat <<'EOF'
Usage: publish-lab-catalog.sh [--dry-run] [--file <labs.json>]
       publish-lab-catalog.sh --check

  (no option)    build, validate and publish the catalog (PUT /api/catalog)
  --dry-run      build, validate and print the payload; nothing is sent
  --file <path>  read another file than Dashboard/LabDashboard/labs.json (tests)
  --check        print the catalog currently published (GET /api/catalog)

Environment, except for --dry-run: ADMIN_DASHBOARD_URL, CATALOG_PUBLISH_TOKEN.
EOF
}

in_ci() { [ "${GITHUB_ACTIONS:-}" = "true" ]; }

# Workflow commands end at the first line break: escape it so a message stays one annotation.
escape_command() {
  local s=$1
  s=${s//%/%25}
  s=${s//$'\r'/%0D}
  s=${s//$'\n'/%0A}
  printf '%s' "$s"
}

info() { printf '%s\n' "$*" >&2; }

error() {
  if in_ci; then printf '::error::%s\n' "$(escape_command "$*")" >&2; else printf 'error: %s\n' "$*" >&2; fi
}

warning() {
  if in_ci; then printf '::warning::%s\n' "$(escape_command "$*")" >&2; else printf 'warning: %s\n' "$*" >&2; fi
}

die() {
  error "$@"
  exit 1
}

# --- Arguments ---------------------------------------------------------------

mode=publish
file=""
while [ $# -gt 0 ]; do
  case $1 in
    --dry-run) [ "$mode" = check ] && die "--dry-run and --check cannot be combined"; mode=dry-run ;;
    --check) [ "$mode" = dry-run ] && die "--dry-run and --check cannot be combined"; mode=check ;;
    --file)
      [ $# -ge 2 ] || die "--file needs a path"
      file=$2
      shift
      ;;
    -h | --help) usage; exit 0 ;;
    *) usage >&2; die "unknown argument: $1" ;;
  esac
  shift
done

command -v jq >/dev/null || die "jq is required"
command -v git >/dev/null || die "git is required"

# Resolve --file against the caller's directory, before anything else uses a path.
if [ -n "$file" ]; then
  case $file in /*) ;; *) file="$PWD/$file" ;; esac
fi

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
git -C "$repo_root" rev-parse --git-dir >/dev/null 2>&1 || die "$repo_root is not a git repository"
[ -n "$file" ] || file="$repo_root/$SOURCE_PATH"

tmp_dir=$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/lab-catalog.XXXXXX")
trap 'rm -rf "$tmp_dir"' EXIT

# --- Admin dashboard settings ------------------------------------------------

base_url=""

require_settings() {
  local url=${ADMIN_DASHBOARD_URL:-}
  local token=${CATALOG_PUBLISH_TOKEN:-}
  url=${url%/}
  [ -n "$url" ] || die "ADMIN_DASHBOARD_URL is not set (e.g. https://labs-admin-dash.vercel.app)"
  [ -n "$token" ] || die "CATALOG_PUBLISH_TOKEN is not set"
  # The token goes only to HTTPS, or to a dashboard running on this machine.
  local https_url='^https://[^[:space:]?#]+$'
  local local_url='^http://(localhost|127\.0\.0\.1)(:[0-9]+)?(/[^[:space:]?#]*)?$'
  if ! [[ $url =~ $https_url || $url =~ $local_url ]]; then
    die "ADMIN_DASHBOARD_URL must be an https:// URL (http:// only for localhost): $url"
  fi
  # Checked here so that a malformed token never reaches the server (10 refusals a minute block the IP).
  local token_format='^[0-9a-fA-F]{64}$'
  [[ $token =~ $token_format ]] || die "CATALOG_PUBLISH_TOKEN must be 64 hexadecimal characters (its value is not shown)"
  base_url=$url
}

# The token goes through curl's configuration on stdin, never on the command line (visible in ps).
# curl --retry retries 408, 429 and 5xx only, honouring Retry-After.
api() {
  local method=$1 path=$2 out=$3
  shift 3
  printf 'header = "Authorization: Bearer %s"\n' "$CATALOG_PUBLISH_TOKEN" |
    curl -sS -K - -X "$method" "$base_url$path" \
      --connect-timeout 10 --max-time 30 \
      --retry 3 --retry-max-time 60 \
      -o "$out" -w '%{http_code}' "$@"
}

# Prints error, message and details of a refused request, then a hint for the status code.
report_failure() {
  local code=$1 body=$2
  if ! jq -e 'type == "object"' "$body" >/dev/null 2>&1; then
    error "HTTP $code with a response that is not JSON: $(head -c 300 "$body" | tr '\r\n' '  ')"
  else
    error "HTTP $code $(jq -r '[.error // empty, .message // empty] | join(": ")' "$body")"
    if jq -e '.details.issues | type == "array" and length > 0' "$body" >/dev/null; then
      while IFS= read -r issue; do
        error "$issue"
      done < <(jq -r '.details.issues[] | "\(.field // "?"): \(.reason // "?")"' "$body")
    elif jq -e '.details != null' "$body" >/dev/null; then
      error "details: $(jq -c '.details' "$body")"
    fi
  fi
  case $code in
    401) info "The token was refused: check CATALOG_PUBLISH_TOKEN. Not retried: 10 refusals a minute block the IP." ;;
    404) info "Route not found: check ADMIN_DASHBOARD_URL." ;;
    409) info "A catalog from a more recent commit is already published. To go back, commit a revert instead of publishing an old commit." ;;
    429) info "Still rate limited after the retries: run it again later." ;;
    503) info "Catalog publishing is not configured on the admin dashboard (its publish token is missing): tell the admin dashboard owner." ;;
  esac
}

# --- --check -----------------------------------------------------------------

if [ "$mode" = check ]; then
  require_settings
  command -v curl >/dev/null || die "curl is required"
  response="$tmp_dir/response.json"
  rc=0
  code=$(api GET /api/catalog "$response") || rc=$?
  [ "$rc" -eq 0 ] || die "GET $base_url/api/catalog failed (curl exit code $rc)"
  if [ "$code" = 404 ] && jq -e '.error == "notFound"' "$response" >/dev/null 2>&1; then
    info "No catalog has been published yet on $base_url."
    exit 0
  fi
  if [ "$code" != 200 ]; then
    report_failure "$code" "$response"
    exit 1
  fi
  jq -r '
    "version      \(.version)",
    "labCount     \(.labCount)",
    "publishedAt  \(.publishedAt)",
    "commit       \(.source.commit // "?") (\(.source.committedAt // "?"))",
    "ids          \(.labs | length):",
    (.labs | to_entries[] | "  \(.key + 1 | tostring | if length < 2 then " " + . else . end). \(.value.id)")
  ' "$response"
  exit 0
fi

# --- Build the payload -------------------------------------------------------

[ -f "$file" ] || die "$file not found"
jq -e '.labs | type == "array"' "$file" >/dev/null 2>&1 || die "$file: not JSON, or .labs is not an array"

git_in_repo() { git -C "$repo_root" "$@"; }

repository=${GITHUB_REPOSITORY:-}
if [ -z "$repository" ]; then
  origin=$(git_in_repo remote get-url origin 2>/dev/null) || die "no remote named origin: set GITHUB_REPOSITORY=owner/name"
  # git@host:owner/name.git, https://host/owner/name(.git), ssh://git@host/owner/name.git → owner/name
  repository=$(printf '%s' "$origin" | sed -E 's#\.git/?$##; s#/+$##; s#^.*[:/]([^/:]+)/([^/:]+)$#\1/\2#')
fi

ref=${GITHUB_REF:-}
if [ -z "$ref" ]; then
  branch=$(git_in_repo symbolic-ref --quiet --short HEAD) || die "detached HEAD: check out a branch or set GITHUB_REF"
  ref="refs/heads/$branch"
fi

commit=${GITHUB_SHA:-}
[ -n "$commit" ] || commit=$(git_in_repo rev-parse HEAD)
committed_at=$(git_in_repo log -1 --format=%cI "$commit") || die "commit $commit not found in $repo_root"

source_json=$(jq -n \
  --arg repository "$repository" --arg ref "$ref" --arg commit "$commit" \
  --arg path "$SOURCE_PATH" --arg committedAt "$committed_at" \
  '{repository: $repository, ref: $ref, commit: $commit, path: $path, committedAt: $committedAt}')

payload="$tmp_dir/payload.json"
# Only the fields of the contract are kept; anything malformed is copied as is, for the validation to report it.
jq -c --argjson source "$source_json" '
  def blank: type == "string" and (sub("^\\s+"; "") | sub("\\s+$"; "")) == "";
  def present: . != null and (blank | not);
  def lab_translations:
    if type != "object" then .
    else
      with_entries(.value |= (
        if type == "object"
        then with_entries(select((.key | IN("track", "title", "level", "summary")) and (.value | present)))
        else . end))
      | with_entries(select(.value != {}))
    end;

  {
    schemaVersion: 1,
    source: $source,
    labs: [
      .labs[]
      | if type != "object" then .
        else
          (.translations | if . == null then {} else lab_translations end) as $translations
          | {id, number, track, title, level, interactive: (.interactive == true)}
            + (if .summary | present then {summary} else {} end)
            + (if $translations == {} then {} else {translations: $translations} end)
        end
    ]
  }
' "$file" >"$payload"

# --- Validate (same rules as the server) ---------------------------------------

issues=$(jq -r '
  def trimmed: sub("^\\s+"; "") | sub("\\s+$"; "");
  # UTF-16 code units, like String.length in JavaScript: never shorter than the count in characters.
  def utf16_length: [explode[] | if . > 65535 then 2 else 1 end] | add // 0;
  def matches($re): type == "string" and test($re);
  def text($field; $max; $required):
    if . == null then (if $required then "\($field): is required" else empty end)
    elif type != "string" then "\($field): must be a string, not \(type)"
    else trimmed as $t
      | if $t == "" then "\($field): must not be empty"
        elif ($t | utf16_length) > $max then "\($field): \($t | utf16_length) characters, \($max) at most"
        else empty end
    end;
  def translation($field):
    if type != "object" then "\($field): must be an object"
    else
      (.track | text("\($field).track"; 64; false)),
      (.title | text("\($field).title"; 200; false)),
      (.level | text("\($field).level"; 32; false)),
      (.summary | text("\($field).summary"; 2000; false))
    end;

  (if .schemaVersion == 1 then empty else "schemaVersion: must be 1" end),
  (if .source.repository | matches("^[A-Za-z0-9_.-]{1,100}/[A-Za-z0-9_.-]{1,100}$") then empty
   else "source.repository: \(.source.repository | tojson) is not owner/name" end),
  (if .source.commit | matches("^[0-9a-fA-F]{40}$") then empty
   else "source.commit: \(.source.commit | tojson) is not 40 hexadecimal characters" end),
  (if .source.committedAt | matches("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]+)?(Z|[+-][0-9]{2}:[0-9]{2})$") then empty
   else "source.committedAt: \(.source.committedAt | tojson) is not an ISO 8601 date with a time zone" end),
  (.labs | length | if . < 1 or . > 200 then "labs: \(.) labs, between 1 and 200 expected" else empty end),
  (.labs | to_entries[] | "labs.\(.key)" as $p | .value
    | if type != "object" then "\($p): must be an object"
      else
        (if .id | matches("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$") then empty
         else "\($p).id: \(.id | tojson | .[0:80]) does not match ^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$" end),
        (.number | text("\($p).number"; 16; true)),
        (.track | text("\($p).track"; 64; true)),
        (.title | text("\($p).title"; 200; true)),
        (.level | text("\($p).level"; 32; true)),
        (.summary | text("\($p).summary"; 2000; false)),
        (.translations
          | if . == null then empty
            elif type != "object" then "\($p).translations: must be an object"
            else
              (length | if . > 20 then "\($p).translations: \(.) languages, 20 at most" else empty end),
              (to_entries[] | "\($p).translations.\(.key)" as $tp
                | (if .key | test("^[a-z]{2}(-[A-Z]{2})?$") then empty
                   else "\($tp): the language must match ^[a-z]{2}(-[A-Z]{2})?$" end),
                  (.value | translation($tp)))
            end)
      end),
  ([.labs | to_entries[] | select((.value | type) == "object" and (.value.id | type) == "string")
    | {index: .key, id: .value.id, key: (.value.id | ascii_downcase)}]
    | group_by(.key)[] | select(length > 1) | .[0] as $first | .[1:][]
    | "labs.\(.index).id: \(.id | tojson) duplicates labs.\($first.index).id \($first.id | tojson) (ids are compared ignoring case)")
' "$payload")

body_bytes=$(wc -c <"$payload" | tr -d ' ')
if [ "$body_bytes" -gt "$MAX_BODY_BYTES" ]; then
  [ -z "$issues" ] || issues+=$'\n'
  issues+="body: $body_bytes bytes, $MAX_BODY_BYTES at most"
fi

if [ -n "$issues" ]; then
  count=0
  while IFS= read -r issue; do
    error "$issue"
    count=$((count + 1))
  done <<<"$issues"
  die "$file: $count problem(s), nothing published"
fi

# A renamed or removed id cuts the link with the progress already recorded for that lab.
previous="$tmp_dir/previous.json"
if git_in_repo show "$commit~1:$SOURCE_PATH" >"$previous" 2>/dev/null; then
  removed=$(jq -r --slurpfile previous "$previous" '
    ([$previous[0].labs[]? | objects | .id | strings] - [.labs[] | objects | .id | strings]) | join(", ")
  ' "$payload" 2>/dev/null) || removed=""
  if [ -n "$removed" ]; then
    warning "ids in $SOURCE_PATH at ${commit:0:7}~1 but no longer here: $removed. Renaming a lab id cuts the link with the progress already recorded by the participants."
  fi
fi

lab_count=$(jq '.labs | length' "$payload")

# --- Dry run -----------------------------------------------------------------

if [ "$mode" = dry-run ]; then
  jq . "$payload"
  info ""
  info "Payload valid (dry run, nothing sent): $lab_count labs, $body_bytes bytes, commit $commit ($committed_at), ref $ref."
  jq -r '.labs | to_entries[] | "  \(.key + 1 | tostring | if length < 2 then " " + . else . end). \(.value.id)"' "$payload" >&2
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    {
      printf '### Lab catalog: dry run\n\n'
      printf 'Valid payload, nothing sent: %s labs, %s bytes, commit `%s`.\n\n' "$lab_count" "$body_bytes" "$commit"
      jq -r '.labs[] | "1. `\(.id)`"' "$payload"
    } >>"$GITHUB_STEP_SUMMARY"
  fi
  exit 0
fi

# --- Publish -----------------------------------------------------------------

require_settings
command -v curl >/dev/null || die "curl is required"

# The source announces a commit: the content sent must be the file of that commit.
if ! git_in_repo show "$commit:$SOURCE_PATH" 2>/dev/null | cmp -s - "$file"; then
  die "$file differs from $SOURCE_PATH at commit ${commit:0:7} (uncommitted changes?): commit them, or use --dry-run"
fi

response="$tmp_dir/response.json"
rc=0
code=$(api PUT /api/catalog "$response" \
  -H "Content-Type: application/json; charset=utf-8" \
  --data-binary @"$payload") || rc=$?
[ "$rc" -eq 0 ] || die "PUT $base_url/api/catalog failed (curl exit code $rc)"

case $code in
  200 | 201)
    jq -e 'type == "object"' "$response" >/dev/null 2>&1 || die "HTTP $code with a response that is not JSON"
    status=$(jq -r '.status' "$response")
    version=$(jq -r '.version' "$response")
    published_count=$(jq -r '.labCount' "$response")
    published_at=$(jq -r '.publishedAt' "$response")
    info "HTTP $code $status: version $version, $published_count labs, published at $published_at, commit $commit."
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
      {
        printf '### Lab catalog: %s\n\n' "$status"
        printf '| Status | Version | Labs | Commit |\n|---|---|---|---|\n'
        printf '| `%s` (HTTP %s) | `%s` | %s | `%s` |\n' "$status" "$code" "$version" "$published_count" "$commit"
      } >>"$GITHUB_STEP_SUMMARY"
    fi
    ;;
  *)
    report_failure "$code" "$response"
    exit 1
    ;;
esac
