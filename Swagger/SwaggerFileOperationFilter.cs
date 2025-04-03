using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace IA.WebAPI.Swagger
{
    public class SwaggerFileOperationFilter : IOperationFilter
    {
        // Permite subir videos desde swagger
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var formParams = context.MethodInfo.GetParameters()
                .Where(p => p.GetCustomAttribute<FromFormAttribute>() != null)
                .ToList();

            if (formParams.Count != 0)
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = formParams.ToDictionary(
                                    p => p.Name!,
                                    p => new OpenApiSchema
                                    {
                                        Type = p.ParameterType == typeof(IFormFile) ? "string" : "string",
                                        Format = p.ParameterType == typeof(IFormFile) ? "binary" : null
                                    }
                                ),
                                Required = formParams.Select(p => p.Name).ToHashSet()
                            }
                        }
                    }
                };
            }
        }
    }
}
