
#region usings
using SlimDX.Direct3D9;
using System;
using System.Collections;
using System.Collections.Generic;
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
            public bool ShowText;
            public int FontSize;
        }

        [Input("Width", DefaultValue = 64, MinValue = 1)]
        public ISpread<int> FWidthIn;

        [Input("Height", DefaultValue = 64, MinValue = 1)]
        public ISpread<int> FHeightIn;

        [Input("Data", DefaultString = "00000000000")]
        public ISpread<string> FDataIn;

        [Input("Format", DefaultNodeValue = BarcodeFormat.UPC_A)]
        public ISpread<BarcodeFormat> FFormatIn;

        [Input("ShowText", DefaultBoolean = false)]
        public ISpread<bool> FShowTextIn;

        [Input("FontSize", DefaultValue = 32)]
        public ISpread<int> FFontSizeIn;

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

        List<Bitmap> bitmaps;
        BitmapRenderer renderer;
        bool firstFrame = true;
        int spreadMax;

        //called when data for any output pin is requested
        public void Evaluate(int spreadMax)
        {
            if (bitmaps == null || spreadMax != this.spreadMax)
                bitmaps = new List<Bitmap>(spreadMax);
            this.spreadMax = spreadMax;
            FTextureOut.ResizeAndDispose(spreadMax, CreateTextureResource);
            for (int i = 0; i < spreadMax; i++)
            {
                var textureResource = FTextureOut[i];
                if (FWidthIn[i] > 0 && FHeightIn[i] > 0 && FFontSizeIn[i] > 0)
                {
                    renderer = new BitmapRenderer();
                    renderer.TextFont = new System.Drawing.Font("Arial", FFontSizeIn[i]);
                    var info = textureResource.Metadata;
                    //recreate textures if resolution was changed
                    if (info.Width != FWidthIn[i] || info.Height != FHeightIn[i])
                    {
                        textureResource.Dispose();
                        textureResource = CreateTextureResource(i);
                        info = textureResource.Metadata;
                    }
                    if (info.Data != FDataIn[i] || info.Format != FFormatIn[i] ||
                        info.Width != FWidthIn[i] || info.Height != FHeightIn[i] ||
                        info.ShowText != FShowTextIn[i] || info.FontSize != FFontSizeIn[i] || firstFrame)
                    {
                        info.Data = FDataIn[i];
                        info.Format = FFormatIn[i];
                        info.ShowText = FShowTextIn[i];
                        info.FontSize = FFontSizeIn[i];
                        Bitmap bmp = new Bitmap(GenerateBarcodeImage(FWidthIn[i], FHeightIn[i], FDataIn[i], FFormatIn[i], !FShowTextIn[i], FMarginIn[i]));
                        if (bitmaps.Count <= i)
                            bitmaps.Add(bmp);
                        else
                            bitmaps[i] = bmp;
                        textureResource.NeedsUpdate = true;

                    }
                    else
                    {
                        textureResource.NeedsUpdate = false;
                    }
                }
                FTextureOut[i] = textureResource;
            }
            firstFrame = false;
        }

        private Bitmap GenerateBarcodeImage(int width, int height, string data, BarcodeFormat format, bool showText, int margin)
        {
            try
            {
                var cleanData = validateData(data, format);
                var writer = new BarcodeWriter
                {
                    Format = format,
                    Options = EncodingOptions ?? new EncodingOptions
                    {
                        Height = height,
                        Width = width,
                        PureBarcode = showText,
                    },
                    Renderer = renderer
                };
                return writer.Write(cleanData);
            }
            catch (Exception e)
            {
                //throw e;
                return new Bitmap(width, height);
            }
        }

        private string validateData(string data, BarcodeFormat format){
            var result = data;
            var dataLength = 1;
            switch (format)
            {
                case BarcodeFormat.CODABAR:
                    dataLength = 3;
                    break;
                case BarcodeFormat.UPC_E:
                    dataLength = 6;
                    break;
                case BarcodeFormat.UPC_A:
                    dataLength = 11;
                    break;
                case BarcodeFormat.EAN_13:
                    dataLength = 12;
                    break;
                default:
                    dataLength = 1;
                    break;
            }

            if (data.Length > dataLength)
                result = data.Substring(0, dataLength);
            while (result.Length < dataLength)
            {
                result = result.Insert(0, "0");
            }
            return result;
        }

        TextureResource<Info> CreateTextureResource(int slice)
        {
            var info = new Info() {
                Slice = slice,
                Width = FWidthIn[slice],
                Height = FHeightIn[slice],
                Data = FDataIn[slice],
                Format = FFormatIn[slice]
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
            if (bitmaps.Count == spreadMax)
                TextureUtils.CopyBitmapToTexture(bitmaps[info.Slice], texture);
        }
    }
}