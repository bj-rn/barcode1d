
#region usings
using System;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;

using FeralTic.DX11.Resources;

using ZXing;
using ZXing.Common;
using ZXing.Rendering;
#endregion usings

namespace VVVV.DX11.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "Barcode1d",
                Category = "DX11.Texture",
                Version = "",
                Help = "Generates an DX11 texture containing the barcode for the provided data in the specified format.",
                Tags = "barcode, ZXing",
                Author = "bj-rn",
                Credits = "ravazquez, vux, ZXing.Net")]
    #endregion PluginInfo
    public class Barcode1dDX11 : IPluginEvaluate, IDX11ResourceHost, IDisposable
    {

        [Input("Size", DefaultValues = new double[] {64, 64}, MinValue = 1, AsInt = true)]
        protected IDiffSpread<Vector2D> FSizeIn;

        [Input("Data", DefaultString = "00000000000000")]
        protected IDiffSpread<string> FDataIn;

        [Input("Format", DefaultNodeValue = BarcodeFormat.UPC_A)]
        protected IDiffSpread<BarcodeFormat> FFormatIn;

        [Input("ShowText", DefaultBoolean = false)]
        protected IDiffSpread<bool> FShowTextIn;

        [Input("Font", EnumName = "SystemFonts")]
        protected IDiffSpread<EnumEntry> FFontIn;

        [Input("FontSize", DefaultValue = 12)]
        protected IDiffSpread<int> FFontSizeIn;

        [Output("Texture Out")]
        protected ISpread<DX11Resource<DX11Texture2D>> FTextureOut;

        [Output("Status")]
        protected ISpread<String> FStatusOut;

        [Import]
        protected ILogger FLogger;

        private EncodingOptions EncodingOptions { get; set; }
        protected BitmapRenderer renderer;

        public void Evaluate(int spreadMax)
        {
            if (FSizeIn.IsChanged || FDataIn.IsChanged || FFormatIn.IsChanged 
                || FShowTextIn.IsChanged || FFontIn.IsChanged ||FFontSizeIn.IsChanged)

            {
                for (int i = 0; i < FTextureOut.SliceCount; i++)
                {
                    if (FTextureOut[i] != null)
                    {
                        FTextureOut[i].Dispose();
                    }
                }
            }

            FTextureOut.SliceCount = spreadMax;
            FStatusOut.SliceCount = spreadMax;
            for (int i = 0; i < FTextureOut.SliceCount; i++)
            {
                if (FTextureOut[i] == null)
                {
                    FTextureOut[i] = new DX11Resource<DX11Texture2D>();
                }
            }
        }

        private Bitmap GenerateBarcodeImage(int currentSlice)
        {
            int w = (int)FSizeIn[currentSlice].x;
            int h = (int)FSizeIn[currentSlice].y;

            try
            {
                var writer = new BarcodeWriter
                {
                    Format = FFormatIn[currentSlice],
                    Options = EncodingOptions ?? new EncodingOptions
                    {
                        Width = w,
                        Height = h,
                        PureBarcode = !FShowTextIn[currentSlice],
                    },
                    Renderer = renderer
                };
                FStatusOut[currentSlice] = "Success";
                return writer.Write(FDataIn[currentSlice]);
            }
            catch (Exception e)
            {
                FStatusOut[currentSlice] = e.Message;
                return new Bitmap(w, h);
            }
        }

        public void Update(FeralTic.DX11.DX11RenderContext context)
        {
            for (int i = 0; i < FTextureOut.SliceCount; i++)
            {
                if (!FTextureOut[i].Contains(context))
                {
                    renderer = new BitmapRenderer();
                    renderer.TextFont = new System.Drawing.Font(FFontIn[i].Name, FFontSizeIn[i]);

                    Bitmap bmp = new Bitmap (GenerateBarcodeImage(i));

                    DX11DynamicTexture2D tex = new DX11DynamicTexture2D(context, bmp.Width, bmp.Height, SlimDX.DXGI.Format.R8G8B8A8_UNorm);

                    int pitch = tex.GetRowPitch();

                    var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);

                    if (pitch != bmp.Width * 4)
                    {
                        tex.WriteDataPitch(data.Scan0, bmp.Width * bmp.Height * 4);
                    }
                    else
                    {
                        tex.WriteData(data.Scan0, bmp.Width * bmp.Height * 4);
                    }

                    FTextureOut[i][context] = tex;

                }
            }
        }

        public void Destroy(FeralTic.DX11.DX11RenderContext context, bool force)
        {
            FTextureOut.SafeDisposeAll(context);
        }

        public void Dispose()
        {
            FTextureOut.SafeDisposeAll();
        }
    }
}