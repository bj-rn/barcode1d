
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
using VVVV.DX11;

using ZXing;
using ZXing.Common;
using ZXing.Rendering;
#endregion usings

namespace VVVV.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "Barcode1d",
                Category = "DX11.Texture",
                Version = "",
                Help = "Generates an DX11 texture containing the barcode for the provided data in the specified format.",
                Tags = "barcode",
                Author = "bj-rn",
                Credits = "ravazquez, vux, ZXing.Net")]
    #endregion PluginInfo
    public class Barcode1dDX11 : IPluginEvaluate, IDX11ResourceHost, IDisposable
    {

        [Input("Size", DefaultValues = new double[] {64, 64}, DimensionNames = new string[] { "Width", "Height" }, MinValue = 1, AsInt = true)]
        public IDiffSpread<Vector2D> FSizeIn;

        [Input("Data", DefaultString = "00000000000000")]
        public IDiffSpread<string> FDataIn;

        [Input("Format", DefaultNodeValue = BarcodeFormat.UPC_A)]
        public IDiffSpread<BarcodeFormat> FFormatIn;

        [Output("Texture Out")]
        public ISpread<DX11Resource<DX11Texture2D>> FTextureOut;

        [Output("Status")]
        public ISpread<String> FStatusOut;

        [Import]
        public ILogger FLogger;


        private EncodingOptions EncodingOptions { get; set; }
        BitmapRenderer renderer;


        //called when data for any output pin is requested
        public void Evaluate(int spreadMax)
        {
            if (FSizeIn.IsChanged || FDataIn.IsChanged || FFormatIn.IsChanged)

            {
                for (int i = 0; i < this.FTextureOut.SliceCount; i++)
                {
                    if (this.FTextureOut[i] != null)
                    {
                        this.FTextureOut[i].Dispose();
                    }
                }
            }

            this.FTextureOut.SliceCount = spreadMax;
            for (int i = 0; i < this.FTextureOut.SliceCount; i++)
            {
                if (this.FTextureOut[i] == null)
                {
                    this.FTextureOut[i] = new DX11Resource<DX11Texture2D>();
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
            for (int i = 0; i < this.FTextureOut.SliceCount; i++)
            {
                if (!this.FTextureOut[i].Contains(context))
                {
                    renderer = new BitmapRenderer();
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

                    this.FTextureOut[i][context] = tex;

                }
            }
        }

        public void Destroy(FeralTic.DX11.DX11RenderContext context, bool force)
        {
            this.FTextureOut.SafeDisposeAll(context);
        }

        public void Dispose()
        {
            this.FTextureOut.SafeDisposeAll();
        }
    }
}