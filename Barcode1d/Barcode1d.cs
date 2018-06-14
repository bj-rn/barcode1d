
#region usings
using SlimDX.Direct3D9;
using System;
using System.ComponentModel.Composition;
using System.Drawing;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.EX9;
using VVVV.Utils.SlimDX;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
#endregion usings

//here you can change the vertex type

namespace VVVV.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "Barcode1d",
                Category = "EX9.Texture",
                Version = "",
                Help = "Basic template which creates a texture",
                Tags = "c#")]
    #endregion PluginInfo
    public class Barcode1d : IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        //little helper class used to store information for each
        //texture resource
        public class Info
        {
            public int Slice;
            public int Width;
            public int Height;
            public string Data;
            public BarcodeFormat Format;
        }

        [Input("Data", DefaultValue = 1)]
        public ISpread<string> FDataIn;

        [Input("Format", DefaultValue = 1)]
        public ISpread<BarcodeFormat> FFormatIn;

        [Input("Width", DefaultValue = 64, MinValue = 1)]
        public ISpread<int> FWidthIn;

        [Input("Height", DefaultValue = 64, MinValue = 1)]
        public ISpread<int> FHeightIn;

        [Output("Texture Out")]
        public ISpread<TextureResource<Info>> FTextureOut;

        [Import]
        public ILogger FLogger;

        public void OnImportsSatisfied()
        {
            //spreads have a length of one by default, change it
            //to zero so ResizeAndDispose works properly.
            FTextureOut.SliceCount = 0;
        }

        private EncodingOptions EncodingOptions { get; set; }

        Bitmap bitmapData; //ToDo: should be collection

        //called when data for any output pin is requested
        public void Evaluate(int spreadMax)
        {
            FTextureOut.ResizeAndDispose(spreadMax, CreateTextureResource);
            for (int i = 0; i < spreadMax; i++)
            {
                var textureResource = FTextureOut[i];
                var info = textureResource.Metadata;
                //recreate textures if resolution was changed
                if (info.Width != FWidthIn[i] || info.Height != FHeightIn[i])
                {
                    textureResource.Dispose();
                    textureResource = CreateTextureResource(i);
                    info = textureResource.Metadata;
                }
                if (info.Data != FDataIn[i] || info.Format != FFormatIn[i] || info.Width != FWidthIn[i] || info.Height != FHeightIn[i])
                {
                    bitmapData = new Bitmap(GenerateBarcodeImage(FWidthIn[i], FHeightIn[i], FDataIn[i], FFormatIn[i]));
                    textureResource.NeedsUpdate = true;
                }
                else
                {
                    textureResource.NeedsUpdate = false;
                }
                FTextureOut[i] = textureResource;
            }
        }

        private Bitmap GenerateBarcodeImage(int width, int height, string data, BarcodeFormat format)
        {
            try
            {
                var writer = new BarcodeWriter
                {
                    Format = format,
                    Options = EncodingOptions ?? new EncodingOptions
                    {
                        Height = height,
                        Width = width,
                        PureBarcode = true,
                        Margin = 0,

                    },
                    Renderer = (IBarcodeRenderer<Bitmap>)Activator.CreateInstance(typeof(BitmapRenderer))
                };
                return writer.Write(data);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        TextureResource<Info> CreateTextureResource(int slice)
        {
            var info = new Info() {
                Slice = slice,
                Width = FWidthIn[slice],
                Height = FHeightIn[slice]/*,
                Data = FDataIn[slice],
                Format = FFormatIn[slice]*/
            };
            return TextureResource.Create(info, CreateTexture, UpdateTexture);
        }

        //this method gets called, when Reinitialize() was called in evaluate,
        //or a graphics device asks for its data
        Texture CreateTexture(Info info, Device device)
        {
            FLogger.Log(LogType.Debug, "Creating new texture at slice: " + info.Slice);
            return TextureUtils.CreateTexture(device, Math.Max(info.Width, 1), Math.Max(info.Height, 1));
        }

        //this method gets called, when Update() was called in evaluate,
        //or a graphics device asks for its texture, here you fill the texture with the actual data
        //this is called for each renderer, careful here with multiscreen setups, in that case
        //calculate the pixels in evaluate and just copy the data to the device texture here
        unsafe void UpdateTexture(Info info, Texture texture)
        {
            TextureUtils.CopyBitmapToTexture(bitmapData, texture);
            //TextureUtils.Fill32BitTexInPlace(texture, info, null);
        }
    }
}