using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: JitSourceMap <assembly> <pdb> <declaring-type> <method>");
    return 2;
}

var assemblyPath = args[0];
var pdbPath = args[1];
var requestedType = StripInstantiation(args[2]);
var requestedMethod = StripInstantiation(args[3]);

using var assemblyStream = File.OpenRead(assemblyPath);
using var peReader = new PEReader(assemblyStream);
var metadata = peReader.GetMetadataReader();

using var pdbStream = File.OpenRead(pdbPath);
using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
var pdb = pdbProvider.GetMetadataReader();

var candidates = new List<(MethodDefinitionHandle Handle, string TypeName)>();
foreach (var handle in metadata.MethodDefinitions)
{
    var definition = metadata.GetMethodDefinition(handle);
    if (!string.Equals(metadata.GetString(definition.Name), requestedMethod, StringComparison.Ordinal))
    {
        continue;
    }

    var typeName = GetTypeName(metadata, definition.GetDeclaringType());
    if (string.Equals(typeName, requestedType, StringComparison.Ordinal)
        || typeName.EndsWith(requestedType, StringComparison.Ordinal)
        || requestedType.EndsWith(typeName, StringComparison.Ordinal))
    {
        candidates.Add((handle, typeName));
    }
}

if (candidates.Count != 1)
{
    Console.Error.WriteLine(
        $"Expected one PDB method for '{requestedType}:{requestedMethod}', found {candidates.Count}.");
    return 3;
}

var debugHandle = MetadataTokens.MethodDebugInformationHandle(
    MetadataTokens.GetRowNumber(candidates[0].Handle));
var debugInformation = pdb.GetMethodDebugInformation(debugHandle);
var points = new List<SourcePoint>();

foreach (var point in debugInformation.GetSequencePoints())
{
    if (point.IsHidden)
    {
        continue;
    }

    var documentHandle = point.Document.IsNil ? debugInformation.Document : point.Document;
    if (documentHandle.IsNil)
    {
        continue;
    }

    var document = pdb.GetDocument(documentHandle);
    points.Add(new SourcePoint(
        point.Offset,
        pdb.GetString(document.Name),
        point.StartLine,
        point.StartColumn));
}

Console.WriteLine(JsonSerializer.Serialize(points));
return 0;

static string GetTypeName(MetadataReader metadata, TypeDefinitionHandle handle)
{
    var definition = metadata.GetTypeDefinition(handle);
    var name = metadata.GetString(definition.Name);
    var declaring = definition.GetDeclaringType();
    if (!declaring.IsNil)
    {
        return $"{GetTypeName(metadata, declaring)}+{name}";
    }

    var @namespace = metadata.GetString(definition.Namespace);
    return string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}";
}

static string StripInstantiation(string value)
{
    var bracket = value.IndexOf('[');
    return bracket < 0 ? value : value[..bracket];
}

internal sealed record SourcePoint(int IlOffset, string Document, int Line, int Column);
