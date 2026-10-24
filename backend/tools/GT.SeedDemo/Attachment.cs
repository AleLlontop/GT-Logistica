using GT.Application.Choferes.Documentacion;
using QuestPDF.Fluent;
namespace GT.SeedDemo;
public sealed partial class Dataset
{
    static ArchivoCargado? Attachment(bool enabled)
    {
        if(!enabled)return null;
        var bytes=Document.Create(c=>c.Page(p=>{p.Margin(30);p.Content().Text("Constancia de documentación de transporte · Archivo de referencia sin validez legal");})).GeneratePdf();
        return new("constancia-documentacion.pdf",bytes.Length,()=>new MemoryStream(bytes,false));
    }
}
