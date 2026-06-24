using Scriban;
using Scriban.Runtime;

namespace BaseForge.CodeGen.Generation;

/// <summary>Scriban şablonlarını C# üye adlarıyla (PascalCase) render eden ince sarmalayıcı.</summary>
internal static class TemplateEngine
{
    public static string Render(string templateText, object model)
    {
        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                "Şablon ayrıştırılamadı: " + string.Join("; ", template.Messages.Select(m => m.Message)));
        }

        var context = new TemplateContext { MemberRenamer = member => member.Name };
        var scriptObject = new ScriptObject();
        scriptObject.Import(model, renamer: member => member.Name);
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }
}
