// File: src/PostgresCopy/Migration/VerificationException.cs

namespace PostgresCopy.Migration;

public sealed class VerificationException(string message) : Exception(message);
