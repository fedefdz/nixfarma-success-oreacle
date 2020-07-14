using Sisfarma.RestClient.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sisfarma.Sincronizador.Core.Extensions
{
    public static class ExceptionExtension
    {
        public static string ToLogErrorMessage(this Exception @this) 
            =>  $"Fecha UTC: {DateTime.UtcNow.ToIsoString()}{Environment.NewLine}" +
                $"Message: {@this.ToFormattedString()}{Environment.NewLine}StackTrace: {@this.StackTrace}";

        public static string ToLogErrorMessage(this RestClientException @this)
            => $"Fecha UTC: {DateTime.UtcNow.ToIsoString()}{Environment.NewLine}" +
                $"Request: {@this.Request?.ToString()}{Environment.NewLine}Response: {@this.Response?.ToString()}{Environment.NewLine}" +
                $"Message: {@this.ToFormattedString()}{Environment.NewLine}StackTrace: {@this.StackTrace}";

        public static string ToFormattedString(this Exception exception)
        {
            try
            {
                IEnumerable<string> messages = exception
                .GetAllExceptions()
                .Where(e => !string.IsNullOrWhiteSpace(e.Message))
                .Select(e => e.Message.Trim());
                string flattened = string.Join(" | ", messages); // <-- the separator here
                return flattened;
            }
            catch (Exception)
            {
                if (exception.InnerException != null)
                    return $"{exception.Message} | {exception.InnerException.Message}";
                
                return exception.Message;
            }
            
        }

        public static IEnumerable<Exception> GetAllExceptions(this Exception exception)
        {
            yield return exception;

            if (exception is AggregateException aggrEx)
            {
                foreach (Exception innerEx in aggrEx.InnerExceptions.SelectMany(e => e.GetAllExceptions()))
                {
                    yield return innerEx;
                }
            }
            else if (exception.InnerException != null)
            {
                foreach (Exception innerEx in exception.InnerException.GetAllExceptions())
                {
                    yield return innerEx;
                }
            }
        }
    }
}
