using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using OT.Assessment.App.Requests;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OT.Assessment.App.Swagger;

public sealed class CasinoWagerRequestExample : ISchemaFilter
{
    /// Like writing a sample answer on a blank form, so the tester can press Execute in Swagger without typing anything.
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(CasinoWagerRequest) || schema is not OpenApiSchema wagerSchema)
        {
            return;
        }

        wagerSchema.Example = JsonNode.Parse("""
            {
              "wagerId": "aa6700eb-1a06-483e-9739-d293dc7a9383",
              "theme": "adventure",
              "provider": "Ergonomic Soft Fish",
              "gameName": "Ergonomic Granite Cheese",
              "transactionId": "410b7161-3473-4d74-85c3-a533d050a9d3",
              "brandId": "8a2016f8-c4c4-471f-9a9c-337a54664650",
              "accountId": "5ac75fec-23e9-27d1-b660-179eee70003d",
              "username": "Jay.Bernhard67",
              "externalReferenceId": "0267dbca-2760-4a9e-ab42-5ce766fa8ca0",
              "transactionTypeId": "8aaece0c-5d53-4225-a937-adb454c4da31",
              "amount": 38273.974454660885,
              "createdDateTime": "2024-05-04T02:25:05.9906387+02:00",
              "numberOfBets": 3,
              "countryCode": "BS",
              "duration": 1827254
            }
            """);
    }
}
