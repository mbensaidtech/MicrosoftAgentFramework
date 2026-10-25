namespace AIAgentWebApi;

/// <summary>Body of a message sent to the API: <c>{ "message": "..." }</c>.</summary>
public sealed record ChatRequest(string Message);
