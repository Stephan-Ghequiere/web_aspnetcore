﻿using Digipolis.Errors;
using Digipolis.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Digipolis.Web.Exceptions
{
    public class ExceptionHandler : IExceptionHandler
    {
        private readonly IExceptionMapper _mapper;
        private readonly ILogger<ExceptionHandler> _logger;
        private readonly IOptions<MvcJsonOptions> _options;
        private readonly IOptions<ApiExtensionOptions> _apiExtensionOptions;

        public ExceptionHandler(IExceptionMapper mapper, ILogger<ExceptionHandler> logger, IOptions<MvcJsonOptions> options, IOptions<ApiExtensionOptions> apiExtensionOptions)
        {
            if(mapper == null) throw new ArgumentNullException(nameof(mapper));
            if(logger == null) throw new ArgumentNullException(nameof(logger));

            _mapper = mapper;
            _logger = logger;
            _options = options;
            _apiExtensionOptions = apiExtensionOptions;
        }

        public async Task HandleAsync(HttpContext context, Exception ex)
        {
            if(_apiExtensionOptions?.Value?.DisableGlobalErrorHandling == true) return;

            var error = _mapper?.Resolve(ex);
            if (error == null) return;
            if (!string.IsNullOrWhiteSpace(error.Title) || !string.IsNullOrWhiteSpace(error.Code) || error.Type != null || error.ExtraInfo?.Any() == true)
            {
                context.Response.Clear();
                context.Response.ContentType = "application/problem+json";
                if (error.Status != default(int)) context.Response.StatusCode = error.Status;
                var json = JsonConvert.SerializeObject(error, _options?.Value?.SerializerSettings ?? new JsonSerializerSettings());
                await context.Response.WriteAsync(json);
            }
            else if (error.Status != default(int)) context.Response.StatusCode = error.Status;
            LogException(error, ex);
        }

        public void Handle(HttpContext context, Exception ex)
        {
            if (_apiExtensionOptions?.Value?.DisableGlobalErrorHandling == true) return;

            var error = _mapper?.Resolve(ex);
            if (error == null) return;
            if (!string.IsNullOrWhiteSpace(error.Title) || !string.IsNullOrWhiteSpace(error.Code) || error.Type != null || error.ExtraInfo?.Any() == true)
            {
                context.Response.Clear();
                context.Response.ContentType = "application/problem+json";
                if (error.Status != default(int)) context.Response.StatusCode = error.Status;
                var json = JsonConvert.SerializeObject(error, _options?.Value?.SerializerSettings ?? new JsonSerializerSettings());
                context.Response.WriteAsync(json).Wait();
            }
            else if (error.Status != default(int)) context.Response.StatusCode = error.Status;
            LogException(error, ex);
        }

        private void LogException(Error error, Exception exception)
        {
            var logMessage = new ExceptionLogMessage
            {
                Error = error,
                ExceptionInfo = exception.ToString()
            };

            if ((_apiExtensionOptions.Value?.LogExceptionObject).GetValueOrDefault())
            {
                logMessage.Exception = exception;
            }

            var logAsJson = JsonConvert.SerializeObject(logMessage, _options?.Value?.SerializerSettings ?? new JsonSerializerSettings());
            if (error.Status >= 500 && error.Status <= 599)
                _logger?.LogError(logAsJson);
            else if (error.Status >= 400 && error.Status <= 499)
                _logger?.LogDebug(logAsJson);
            else _logger?.LogInformation(logAsJson);
        }
    }
}
