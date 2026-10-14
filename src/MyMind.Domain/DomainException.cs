namespace MyMind.Domain;

/// <summary>A domain rule was violated; the message is safe to show to the user.</summary>
public sealed class DomainException(string message) : Exception(message);
