# AI Agents Frontend

A modern React + TypeScript + Vite application for AI Agents management.

## 📋 Table of Contents

- [Prerequisites](#prerequisites)
- [Node.js Installation](#nodejs-installation)
- [Project Setup](#project-setup)
- [Running the Application](#running-the-application)
- [Available Scripts](#available-scripts)
- [Build for Production](#build-for-production)
- [Troubleshooting](#troubleshooting)

---

## 📦 Prerequisites

This project requires:
- **Node.js**: Version **20.19+** or **22.12+** (required by Vite 7.x)
- **npm**, **yarn**, or **pnpm** (package managers)

### Check Your Current Node.js Version

```bash
node --version
```

If your Node.js version is below 20.19, follow the installation instructions below.

---

## 🔧 Node.js Installation

### Method 1: Using NVM (Recommended)

**NVM (Node Version Manager)** allows you to easily switch between different Node.js versions.

#### Install NVM

**Linux/macOS:**
```bash
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash
```

After installation, restart your terminal or run:
```bash
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
```

**Windows:**
Download and install [nvm-windows](https://github.com/coreybutler/nvm-windows/releases)

#### Install Node.js 22 (LTS)

```bash
nvm install 22
nvm use 22
nvm alias default 22
```

#### Verify Installation

```bash
node --version  # Should show v22.x.x
npm --version   # Should show v10.x.x
```

---

### Method 2: Direct Installation

Download Node.js directly from the official website:
- [Node.js Official Website](https://nodejs.org/)
- Choose **LTS version 22.x** or higher

**Verify installation:**
```bash
node --version
npm --version
```

---

### Method 3: Using Package Managers

**Ubuntu/Debian:**
```bash
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt-get install -y nodejs
```

**macOS (Homebrew):**
```bash
brew install node@22
brew link --overwrite node@22
```

**Windows (Chocolatey):**
```powershell
choco install nodejs-lts
```

---

## 🚀 Project Setup

### 1. Navigate to Project Directory

```bash
cd /path/to/AIAgentsFrontend
```

### 2. Install Dependencies

Choose your preferred package manager:

#### Using npm (Default)
```bash
npm install
```

#### Using Yarn
```bash
# Install Yarn if not already installed
npm install -g yarn

# Install dependencies
yarn install
```

#### Using pnpm
```bash
# Install pnpm if not already installed
npm install -g pnpm

# Install dependencies
pnpm install
```

---

## ⚙️ Configuration

### Security Configuration (Demo)

The secret key for signing context IDs is configured in:
- **Frontend:** `src/config/security.config.ts`
- **Backend:** `appsettings.json` → `Security.ContextIdSigningKey`

**Demo Key:** `Demo-Secret-Key-2026-For-AI-Agents-Project-Min-32-Chars-Required!`

> 📝 **Note:** For this demo project, the secret key is hardcoded in configuration files. In production, use environment variables and proper secret management (Azure Key Vault, etc.).

See **[CONTEXT_ID_SIGNING.md](../CONTEXT_ID_SIGNING.md)** for complete documentation on the signed context ID mechanism.

---

## 🏃 Running the Application

### Development Mode (with Hot Reload)

Start the development server:

#### Using npm
```bash
npm run dev
```

#### Using Yarn
```bash
yarn dev
```

#### Using pnpm
```bash
pnpm dev
```

The application will start at **http://localhost:3000/** (or another port if 3000 is busy).

### Expose to Network

To access the application from other devices on your network:

```bash
npm run dev -- --host
```

This will display both local and network URLs.

---

## 📜 Available Scripts

| Script | npm | yarn | pnpm | Description |
|--------|-----|------|------|-------------|
| **Development** | `npm run dev` | `yarn dev` | `pnpm dev` | Start development server with HMR |
| **Build** | `npm run build` | `yarn build` | `pnpm build` | Build for production |
| **Preview** | `npm run preview` | `yarn preview` | `pnpm preview` | Preview production build locally |
| **Lint** | `npm run lint` | `yarn lint` | `pnpm lint` | Run ESLint to check code quality |

### Script Details

#### Development Server
```bash
npm run dev
```
- Starts Vite development server
- Hot Module Replacement (HMR) enabled
- Fast refresh for React components
- TypeScript type checking in editor

#### Build for Production
```bash
npm run build
```
- Compiles TypeScript
- Bundles and optimizes for production
- Output directory: `dist/`

#### Preview Production Build
```bash
npm run preview
```
- Serves the production build locally
- Useful for testing before deployment

#### Lint Code
```bash
npm run lint
```
- Runs ESLint on all source files
- Checks for code quality issues

---

## 📦 Build for Production

### 1. Build the Project

```bash
npm run build
```

This creates an optimized production build in the `dist/` directory.

### 2. Preview the Build

```bash
npm run preview
```

Access the preview at **http://localhost:4173/**

### 3. Deploy

Copy the contents of the `dist/` directory to your web server or hosting platform.

**Popular deployment options:**
- **Vercel**: `vercel deploy`
- **Netlify**: Drag & drop `dist/` folder
- **GitHub Pages**: Use `gh-pages` package
- **AWS S3**: Upload `dist/` to S3 bucket
- **Docker**: Serve with nginx or similar

---

## 🛠️ Troubleshooting

### Error: "Vite requires Node.js version X.X+"

**Problem:** Your Node.js version is too old.

**Solution:** Upgrade Node.js using one of the methods above (NVM recommended).

```bash
# Using NVM
nvm install 22
nvm use 22
```

---

### Error: "crypto.hash is not a function"

**Problem:** Node.js version incompatibility.

**Solution:** Ensure you're using Node.js 20.19+ or 22.12+

```bash
node --version
# If version is below 20.19, upgrade Node.js
```

---

### Port Already in Use

**Problem:** Default port 3000 is already occupied.

**Solution:** Vite will automatically try the next available port, or specify a custom port:

```bash
npm run dev -- --port 3001
```

---

### Dependencies Installation Failed

**Problem:** Network issues or corrupted cache.

**Solution:** Clear cache and reinstall:

```bash
# Clear npm cache
npm cache clean --force

# Remove node_modules and package-lock.json
rm -rf node_modules package-lock.json

# Reinstall dependencies
npm install
```

---

### Module Not Found Errors

**Problem:** Missing dependencies or corrupted installation.

**Solution:** Reinstall dependencies:

```bash
rm -rf node_modules package-lock.json
npm install
```

---

### NVM Command Not Found

**Problem:** NVM not loaded in current shell.

**Solution:** Load NVM manually or restart terminal:

```bash
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
```

Or add to your shell profile (`~/.bashrc`, `~/.zshrc`, etc.)

---

## 🏗️ Tech Stack

- **React** 19.2.0 - UI Library
- **TypeScript** 5.9.3 - Type Safety
- **Vite** 7.2.4 - Build Tool & Dev Server
- **React Router DOM** 7.12.0 - Routing
- **ESLint** - Code Quality

---

## 💡 Development Tips

1. **Fast Refresh**: Vite provides instant HMR - changes appear immediately
2. **TypeScript**: Enable strict mode in `tsconfig.json` for better type safety
3. **Code Splitting**: Vite automatically code-splits your application
4. **Environment Variables**: Use `.env` files for configuration (prefix with `VITE_`)
5. **Browser DevTools**: Install React DevTools extension for debugging

---

## 📄 License

This project is part of the Microsoft Agent Framework learning materials.
