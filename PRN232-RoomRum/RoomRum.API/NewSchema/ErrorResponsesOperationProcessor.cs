using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using RoomRum.API.Common;

namespace RoomRum.API.NewSchema;

public sealed class ErrorResponsesOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var operation = context.OperationDescription.Operation;
        var schema = context.SchemaGenerator.Generate(typeof(ApiResponse<object>), context.SchemaResolver);
        void Add(string code, string description)
        {
            operation.Responses.TryAdd(code, new OpenApiResponse
            {
                Description = description,
                Schema = new JsonSchema { Reference = schema }
            });
        }

        Add("default", "Error response using the standard success/message/data envelope.");
        Add("500", "Unexpected server error.");
        Add("503", "Database service unavailable.");

        if (context is AspNetCoreOperationProcessorContext aspNet)
        {
            var method = aspNet.ApiDescription.HttpMethod;
            var hasBody = method is "POST" or "PUT" or "PATCH";
            var hasQuery = aspNet.ApiDescription.ParameterDescriptions.Any(x => x.Source?.Id == "Query");
            var hasRouteParameters = aspNet.ApiDescription.RelativePath?.Contains('{') == true;
            if (hasBody || hasQuery) Add("400", "Invalid request values.");
            if (hasBody) Add("415", "Unsupported request content type.");
            if (hasRouteParameters) Add("404", "Resource not found.");
            if (method is "POST" or "PUT" or "PATCH" or "DELETE")
                Add("409", "Request conflicts with the current state.");
        }

        var anonymous = context.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), true) ||
            context.ControllerType.IsDefined(typeof(AllowAnonymousAttribute), true);
        var secured = !anonymous &&
            (context.MethodInfo.IsDefined(typeof(AuthorizeAttribute), true) ||
             context.ControllerType.IsDefined(typeof(AuthorizeAttribute), true));
        if (secured)
        {
            Add("401", "Authentication is required.");
            Add("403", "Authenticated user lacks permission.");
        }
        if (context.MethodInfo.IsDefined(typeof(EnableRateLimitingAttribute), true) ||
            context.ControllerType.IsDefined(typeof(EnableRateLimitingAttribute), true))
            Add("429", "Request rate limit exceeded.");
        return true;
    }
}
