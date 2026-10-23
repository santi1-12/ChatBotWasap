using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using System.Net;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace WhatsAppBot.Api.ContractTests;

public sealed class OpenApiContractTests
{
    [Fact]
    public void Document_Parses_AndDeclaresTenantConversationOperations()
    {
        var document = LoadDocument();

        Assert.Contains("/api/v1/admin/conversations", document.Paths!.Keys);
        Assert.Contains("/api/v1/admin/conversations/{id}", document.Paths.Keys);
        Assert.Contains("/api/v1/admin/conversations/{id}/actions", document.Paths.Keys);
        Assert.Contains("/api/v1/admin/conversations/{id}/messages", document.Paths.Keys);
        Assert.Contains("/api/v1/admin/messages/{id}/media", document.Paths.Keys);

        AssertResponses(document, "/api/v1/admin/conversations/{id}/actions", HttpMethod.Post,
            HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Conflict);
        AssertResponses(document, "/api/v1/admin/conversations/{id}/messages", HttpMethod.Post,
            HttpStatusCode.Accepted, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Conflict);
        AssertResponses(document, "/api/v1/admin/messages/{id}/media", HttpMethod.Get,
            HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.NotFound,
            HttpStatusCode.Gone);
    }

    [Fact]
    public void Document_DeclaresTenantModalityAndSafeMediaContracts()
    {
        var document = LoadDocument();

        var signup = Schema(document, "EmbeddedSignupCompletionRequestDTO");
        Assert.True(HasRequired(signup, "onboardingType"));
        Assert.True(HasProperty(signup, "phoneNumberId"));

        var integration = Schema(document, "WhatsAppIntegrationDTO");
        Assert.True(HasProperty(integration, "tenantId"));
        Assert.True(HasProperty(integration, "onboardingType"));

        var action = Schema(document, "ConversationActionRequestDTO");
        Assert.True(HasRequired(action, "action"));
        Assert.True(HasRequired(action, "expectedVersion"));

        var manualMessage = Schema(document, "ManualMessageRequestDTO");
        Assert.True(HasRequired(manualMessage, "text"));
        Assert.True(HasRequired(manualMessage, "expectedVersion"));

        var summary = Schema(document, "ConversationSummaryDTO");
        Assert.True(HasRequired(summary, "tenantId"));
        Assert.True(HasRequired(summary, "status"));
        Assert.True(HasRequired(summary, "attentionMode"));
        Assert.True(HasRequired(summary, "lifecycleStatus"));

        var message = Schema(document, "MessageDTO");
        Assert.True(HasProperty(message, "actorType"));
        Assert.True(HasProperty(message, "origin"));
        Assert.True(HasProperty(message, "sourceInboundMessageId"));
        Assert.True(HasProperty(message, "outcomeCode"));
        Assert.True(HasProperty(message, "mediaAvailabilityState"));
        Assert.True(HasProperty(message, "mediaUrl"));
    }

    private static OpenApiDocument LoadDocument()
    {
        var path = FindRepositoryFile(
            "specs", "001-whatsapp-business-bot-poc", "contracts", "openapi.yaml");
        var yaml = File.ReadAllText(path);
        var json = ConvertYamlToJson(yaml);
        var result = OpenApiDocument.Parse(json, "json", new OpenApiReaderSettings());
        Assert.Empty(result.Diagnostic?.Errors ?? []);
        Assert.NotNull(result.Document);
        return result.Document!;
    }

    private static string ConvertYamlToJson(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        var root = ToJsonNode(stream.Documents.Single().RootNode);
        return root!.ToJsonString(new JsonSerializerOptions());
    }

    private static JsonNode? ToJsonNode(YamlNode node) => node switch
    {
        YamlMappingNode mapping => ToJsonObject(mapping),
        YamlSequenceNode sequence => ToJsonArray(sequence),
        YamlScalarNode scalar => ToJsonScalar(scalar),
        _ => throw new InvalidDataException($"Unsupported YAML node: {node.GetType().Name}")
    };

    private static JsonObject ToJsonObject(YamlMappingNode mapping)
    {
        var result = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            var key = ((YamlScalarNode)entry.Key).Value
                ?? throw new InvalidDataException("OpenAPI YAML contains a null mapping key.");
            result[key] = ToJsonNode(entry.Value);
        }

        return result;
    }

    private static JsonArray ToJsonArray(YamlSequenceNode sequence)
    {
        var result = new JsonArray();
        foreach (var child in sequence.Children)
        {
            result.Add(ToJsonNode(child));
        }

        return result;
    }

    private static JsonNode? ToJsonScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style == ScalarStyle.Plain)
        {
            if (value is "null" or "~")
            {
                return null;
            }

            if (bool.TryParse(value, out var boolean))
            {
                return JsonValue.Create(boolean);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return JsonValue.Create(integer);
            }

            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return JsonValue.Create(decimalValue);
            }
        }

        return JsonValue.Create(value);
    }

    private static void AssertResponses(
        OpenApiDocument document,
        string path,
        HttpMethod method,
        params HttpStatusCode[] statuses)
    {
        Assert.True(document.Paths!.TryGetValue(path, out var pathItem), $"Missing path: {path}");
        Assert.True(pathItem!.Operations!.TryGetValue(method, out var operation),
            $"Missing {method} operation for {path}");

        foreach (var status in statuses)
        {
            Assert.Contains(((int)status).ToString(), operation!.Responses!.Keys);
        }
    }

    private static IOpenApiSchema Schema(OpenApiDocument document, string name)
    {
        Assert.True(document.Components!.Schemas!.TryGetValue(name, out var schema),
            $"Missing schema: {name}");
        return schema!;
    }

    private static bool HasRequired(IOpenApiSchema schema, string property) =>
        schema.Required?.Contains(property) == true;

    private static bool HasProperty(IOpenApiSchema schema, string property) =>
        schema.Properties?.ContainsKey(property) == true;

    private static string FindRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativePath)}");
    }
}
