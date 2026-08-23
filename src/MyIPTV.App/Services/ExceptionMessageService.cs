using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.Services;

public sealed class ExceptionMessageService : IExceptionMessageService
{
    public string GetUserMessage(Exception exception, string? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception cause = exception is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions[0]
            : exception;
        return cause switch
        {
            OperationCanceledException => "The operation was canceled or timed out. Please try again.",
            HttpRequestException => "The server could not be reached. Check the address and your internet connection.",
            JsonException => "The server returned data that Noki's IPTV Player could not understand.",
            System.Xml.XmlException => "The guide or playlist data is malformed and could not be read.",
            SqliteException => "The local database could not be updated. Check the application data folder and available disk space.",
            UnauthorizedAccessException => "Windows denied access to an application file. Check the data folder permissions.",
            IOException => "A local or network file could not be read. It may be unavailable or in use.",
            _ => fallback ?? "Something unexpected happened. You can continue using Noki's IPTV Player.",
        };
    }
}
