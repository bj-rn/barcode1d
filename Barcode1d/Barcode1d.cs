#region usings
using System;
using System.ComponentModel.Composition;
using System.Drawing;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.EX9;
using VVVV.Utils.VMath;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
#endregion usings

namespace VVVV.Spreads
{
    public enum TCoordinateSystem { VVVV, OpenCV };

    #region PluginInfo
    [PluginInfo(Name = "Barcode1d", Category = "Barcode", Help = "Creates a barcode image based on the specified format and data.", Author = "ravazquez" ,AutoEvaluate = true)]
    #endregion PluginInfo

    public class Info
    {
        public int Slice;
        public int Width;
        public int Height;
        public double WaveCount;
    }

    public class Barcode1d : IPluginEvaluate, IDisposable
    {
        #region fields & pins
        [Input("Resolution")]
        ISpread<Vector2D> FPinInSensorSize;

        [Input("Width", DefaultValue = 64, MinValue = 1)]
        public ISpread<int> FWidthIn;

        [Input("Height", DefaultValue = 64, MinValue = 1)]
        public ISpread<int> FHeightIn;

        [Input("Enabled")]
        ISpread<bool> FPinEnabled;

        [Output("Texture Out")]
        public ISpread<TextureResource<Info>> FTextureOut;

        [Output("View per board")]
        ISpread<Matrix4x4> FPinOutView;

        [Output("Projection")]
        ISpread<Matrix4x4> FPinOutProjection;

        [Output("Reprojection Error")]
        ISpread<Double> FPinOutError;

        [Output("Success")]
        ISpread<bool> FPinOutSuccess;

        [Output("Status")]
        ISpread<string> FStatus;

        [Import]
        ILogger FLogger;

        #endregion fields & pins

        [ImportingConstructor]
        public Barcode1d(IPluginHost host)
        {

        }

        public void Dispose()
        {

        }

        private EncodingOptions EncodingOptions { get; set; }
        private Type Renderer { get; set; }

        //called when data for any output pin is requested
        /*public void Evaluate(int SpreadMax)
        {
            if (FPinEnabled[0])
            {
                try {
                    FPinOutSuccess[0] = true;
                    FStatus[0] = "OK";

                    var width = 0;
                    var height = 0;
                    var data = "12312312";
                    var writer = new BarcodeWriter
                    {
                        Format = BarcodeFormat.EAN_13,
                        Options = EncodingOptions ?? new EncodingOptions
                        {
                            Height = height,
                            Width = width
                        },
                        Renderer = (IBarcodeRenderer<Bitmap>)Activator.CreateInstance(Renderer)
                    };
                    Image image = writer.Write(data);
                }
                catch (Exception e)
                {
                    FPinOutSuccess[0] = false;
                    FStatus[0] = e.Message;
                }
            }
        }*/

        public void OnImportsSatisfied()
        {
            //spreads have a length of one by default, change it
            //to zero so ResizeAndDispose works properly.
            FTextureOut.SliceCount = 0;
        }

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
                //update textures if their wave count changed
                if (info.WaveCount != FWaveCountIn[i])
                {
                    //info.WaveCount = FWaveCountIn[i];
                    textureResource.NeedsUpdate = true;
                }
                else
                {
                    textureResource.NeedsUpdate = false;
                }
                FTextureOut[i] = textureResource;
            }
        }

        TextureResource<Info> CreateTextureResource(int slice)
        {
            var info = new Info() { Slice = slice, Width = FWidthIn[slice], Height = FHeightIn[slice] };
            return TextureResource.Create(info, CreateTexture, UpdateTexture);
        }
    }
}
