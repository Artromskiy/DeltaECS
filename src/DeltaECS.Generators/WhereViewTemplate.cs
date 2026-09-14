using System;
using System.Text;

namespace Delta.ECS.Generators;

/// <summary>Template for a generated Where view.</summary>
internal static partial class GeneratorTemplates
{
    internal static RenderModel WhereViewTemplate(
        ApiModel api,
        string declaration,
        Action<StringBuilder> render)
        => new(api, RenderBlock(declaration, RenderMember(render)));
}
