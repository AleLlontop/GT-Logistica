using GT.Application.Choferes.Documentacion;
using QuestPDF.Fluent;
namespace GT.SeedDemo;
public sealed partial class Dataset
{
    static ArchivoCargado? Attachment(bool enabled)
    {
        if(!enabled)return null;
        var bytes=Document.Create(c=>c.Page(p=>{p.Margin(30);p.Content().Text("DEMO015 · DOCUMENTO SINTÉTICO SIN VALIDEZ · Sólo desarrollo local");})).GeneratePdf();
        return new("documento-sintetico-DEMO015.pdf",bytes.Length,()=>new MemoryStream(bytes,false));
    }
}
