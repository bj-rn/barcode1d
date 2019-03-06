
#region usings
using SlimDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.EX9;
using VVVV.Utils.SlimDX;
using VVVV.Utils.VMath;
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
                Help = "Generates an EX9 texture containing the barcode for the provided data in the specified format.",
                Tags = "barcode",
                Author = "ravazquez",
                Credits = "thisisyr")]
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


        [Input("Size", DefaultValues = new double[] { 64, 64 }, MinValue = 1, AsInt = true)]
        public ISpread<Vector2D> FSizeIn;

        [Input("Data", DefaultString = "00000000000000")]
        public ISpread<string> FDataIn;

        [Input("Format", DefaultNodeValue = BarcodeFormat.UPC_A)]
        public ISpread<BarcodeFormat> FFormatIn;

        [Input("ShowText", DefaultBoolean = false)]
        public ISpread<bool> FShowTextIn;

        [Input("Font", EnumName = "SystemFonts")]
        IDiffSpread<EnumEntry> FFontIn;

        [Input("FontSize", DefaultValue = 32)]
        public ISpread<int> FFontSizeIn;

        [Output("Texture Out")]
        public ISpread<TextureResource<Info>> FTextureOut;

        [Output("Status")]
        public ISpread<String> FStatusOut;

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
            FStatusOut.SliceCount = spreadMax;
            FTextureOut.ResizeAndDispose(spreadMax, CreateTextureResource);
            for (int i = 0; i < spreadMax; i++)
            {
                var textureResource = FTextureOut[i];

                int w = (int) FSizeIn[i].x;
                int h = (int)FSizeIn[i].y;

                if (w > 0 && h > 0 && FFontSizeIn[i] > 0)
                {
                    renderer = new BitmapRenderer();
                    renderer.TextFont = new System.Drawing.Font(FFontIn[i].Name, FFontSizeIn[i]);
                    var info = textureResource.Metadata;
                    //recreate textures if resolution was changed
                    if (info.Width != w || info.Height != h)
                    {
                        textureResource.Dispose();
                        textureResource = CreateTextureResource(i);
                        info = textureResource.Metadata;
                    }
                    if (info.Data != FDataIn[i] || info.Format != FFormatIn[i] ||
                        info.Width != w || info.Height != h ||
                        info.ShowText != FShowTextIn[i] || info.FontSize != FFontSizeIn[i] || firstFrame)
                    {
                        info.Data = FDataIn[i];
                        info.Format = FFormatIn[i];
                        info.ShowText = FShowTextIn[i];
                        info.FontSize = FFontSizeIn[i];
                        Bitmap bmp = new Bitmap(GenerateBarcodeImage(w, h, FDataIn[i], FFormatIn[i], !FShowTextIn[i], i));
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

        private Bitmap GenerateBarcodeImage(int width, int height, string data, BarcodeFormat format, bool showText, int currentSlice)
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
                        PureBarcode = showText,
                    },
                    Renderer = renderer
                };
                FStatusOut[currentSlice] = "Success";
                return writer.Write(data);
            }
            catch (Exception e)
            {
                FStatusOut[currentSlice] = e.Message;
                return new Bitmap(width, height);
            }
        }

        TextureResource<Info> CreateTextureResource(int slice)
        {
            var info = new Info() {
                Slice = slice,
                Width = (int) FSizeIn[slice].x,
                Height = (int) FSizeIn[slice].y,
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