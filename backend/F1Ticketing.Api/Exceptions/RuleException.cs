namespace F1Ticketing.Api.Exceptions;

// Očekivana poslovna greška se na HTTP nivou pretvara u 400 Bad Request.
public sealed class RuleException(string message) : Exception(message);
