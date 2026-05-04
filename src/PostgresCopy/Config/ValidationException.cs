// File: src/PostgresCopy/Config/ValidationException.cs

namespace PostgresCopy.Config;

public sealed class ValidationException(string message) : Exception(message);
