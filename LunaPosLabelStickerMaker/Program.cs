namespace LunaPosLabelStickerMaker;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test-render")
        {
            var store = new DataStore();
            var prod = store.Products[0];
            var tpl = store.Templates[0];
            var job = new PrintJob
            {
                ProductName = prod.Name,
                SinhalaName = prod.SinhalaName,
                ManufactureDate = new DateTime(2026, 11, 21),
                ExpiryDate = new DateTime(2026, 12, 20),
                BatchNumber = "1121",
                PriceText = "260/=",
                WeightText = "360-380g",
                Description = prod.Description,
                RegistrationNo = prod.RegistrationNo,
                Tel = prod.Tel,
                Template = tpl
            };

            using var bmp = LabelRenderer.RenderLabel(job, 300, isForPrinting: true);
            bmp.Save("test_rendered_label.png", System.Drawing.Imaging.ImageFormat.Png);

            using var preview = LabelRenderer.RenderPreview(job, 600, 500);
            preview.Save("test_rendered_preview.png", System.Drawing.Imaging.ImageFormat.Png);

            Console.WriteLine("TEST_RENDER_OK");
            return;
        }

        ApplicationConfiguration.Initialize();
        TrialManager.Initialize();
        if (TrialManager.IsTrialExpired)
        {
            TrialManager.CheckAccessAndShowDialog(null);
            return;
        }

        Application.Run(new MainForm());
    }    
}