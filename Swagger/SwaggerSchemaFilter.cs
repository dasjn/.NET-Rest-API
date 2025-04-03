using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace IA.WebAPI.Swagger
{
    public class SwaggerSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.GetProperties().Length == 0) return;

            foreach (var prop in context.Type.GetProperties())
            {
                var attribute = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>();
                if (attribute != null)
                {
                    schema.Required.Add(prop.Name);
                }

                var stringLengthAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.StringLengthAttribute>();
                if (stringLengthAttr != null)
                {
                    schema.Properties[prop.Name].MinLength = stringLengthAttr.MinimumLength;
                    schema.Properties[prop.Name].MaxLength = stringLengthAttr.MaximumLength;
                }
            }
        }
    }
}