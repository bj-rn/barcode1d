using System;
using System.Drawing;
using System.IO;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

using SlimDX.Direct3D11;

using VVVV.Core.Logging;

using FeralTic.DX11;
using FeralTic.DX11.Resources;

using ZXing;

namespace VVVV.DX11.Nodes
{
    [PluginInfo(Name = "BarcodeReader", 
        Category = "DX11.Texture", 
        Version = "String",
        Help = "Tries to read barcode(s) from provided textures",
        Tags = "barcode, ZXing",
        Author = "bj-rn", 
        Credits = "kiilo, sebl, vux, ZXing.Net", 
        AutoEvaluate = true)]

    public class Barcode1dDX11Reader : IPluginEvaluate, IDX11ResourceDataRetriever
    {
        [Input("Texture In")]
        protected Pin<DX11Resource<DX11Texture2D>> FTextureIn;

        [Input("Read", IsBang = true)]
        protected ISpread<bool> FRead;

        [Output("Output")]
        public ISpread<ISpread<String>> FOutput;

        [Output("Valid")]
        protected ISpread<ISpread<bool>> FOutValid;

        [Import()]
        protected IPluginHost FHost;

        [Import()]
        protected ILogger FLogger;

        public DX11RenderContext AssignedContext
        {
            get;
            set;
        }

        public event DX11RenderRequestDelegate RenderRequest;

        #region IPluginEvaluate Members

        public void Evaluate(int SpreadMax)
        {
            
            if(FTextureIn.IsChanged)
            {


                for (int i = 0; i < FOutput.SliceCount; i++)
                {
                    FOutput[i].SliceCount = 0;
                    FOutValid[i].SliceCount = 0;
                }


                FOutput.SliceCount = 1;
                FOutValid.SliceCount = 1;
            }

            if (this.FTextureIn.IsConnected)
            {

                RenderRequest?.Invoke(this, FHost);

                if (AssignedContext == null) {FOutValid.SliceCount = 0; return; }
                //Do NOT cache this, assignment done by the host

                var context = AssignedContext;

                FOutput.SliceCount = SpreadMax;
                FOutValid.SliceCount = SpreadMax;

                for (int i = 0; i < SpreadMax; i++)
                {
                    if (FTextureIn[i].Contains(context) && FRead[i])
                    {

                       FOutput[i].SliceCount = 0;
                       FOutValid[i].SliceCount = 0;

                        var reader = new ZXing.BarcodeReader();

                        try
                        {

                            MemoryStream memoryStream = new MemoryStream();
                            Texture2D.ToStream(context.CurrentDeviceContext, FTextureIn[i][context].Resource, SlimDX.Direct3D11.ImageFileFormat.Bmp, memoryStream);

                            Bitmap bmp = new Bitmap(memoryStream);

                            var results = reader.DecodeMultiple(bmp);

                            if (results != null)
                            {
                                var count = results.Length;
                                FOutput[i].SliceCount = count;
                                FOutValid[i].SliceCount = count;

                                for (int j = 0; j < count; j++)
                                {
                                    var result = results[j];
                                    FOutput[i][j] = ((ZXing.Result)(result)).Text;
                                    FOutValid[i][j] = true;
                                }
                                
                            }
                            else
                            {
                                FOutput[i].SliceCount = 1;
                                FOutValid[i].SliceCount = 1;
                                FOutput[i][0] = "";
                                FOutValid[i][0] = false;
                            }

                        
                            
                            
                        }
                        catch (Exception ex)
                        {
                            FLogger.Log(ex);
                            FOutput[i].SliceCount = 1;
                            FOutValid[i].SliceCount = 1;
                            FOutput[i][0] = "";
                            FOutValid[i][0] = false;
                        }
                    }
                   
                }
            }
            else
            {
                FOutput.SliceCount = 1;
                FOutValid.SliceCount = 1;

            }
        }

        #endregion
    }
}
