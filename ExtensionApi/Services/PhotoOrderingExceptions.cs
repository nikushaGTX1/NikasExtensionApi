namespace ExtensionApi.Services;

public sealed class PhotoOrderingNotConfiguredException(string message) : Exception(message);
public sealed class PhotoInputException(string message) : Exception(message);
public sealed class PhotoProviderException(string message, Exception? innerException = null) : Exception(message, innerException);
