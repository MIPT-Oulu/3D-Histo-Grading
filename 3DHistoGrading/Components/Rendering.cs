﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;

using Kitware.VTK;

using HistoGrading.Models;

namespace HistoGrading.Components
{
    /// <summary>
    /// Class for rendering vtkImageData
    /// </summary>
    public class Rendering
    {
        //PipeLines

        /// <summary>
        /// Volume rendering pipeline. Contains methods for connecting components
        /// and memory management.
        /// </summary>
        public class volumePipeLine : IDisposable
        {
            //Volume components

            //VTKVolume
            private vtkVolume vol = vtkVolume.New();
            /// <summary>
            /// Boolean to check if volume is too large to render.
            /// </summary>
            public bool large;
            //Mapper
            private vtkFixedPointVolumeRayCastMapper mapper = vtkFixedPointVolumeRayCastMapper.New();
            //private vtkGPUVolumeRayCastMapper mapper = vtkGPUVolumeRayCastMapper.New();
            private vtkSmartVolumeMapper GPUmapper = vtkSmartVolumeMapper.New();
            //private vtkOpenGLGPUVolumeRayCastMapper mapper = vtkOpenGLGPUVolumeRayCastMapper.New();

            //Colortransfer function for gray values
            private vtkColorTransferFunction ctf = vtkColorTransferFunction.New();
            //Picewise function for opacity
            private vtkPiecewiseFunction spwf = vtkPiecewiseFunction.New();

            //Mask components, same as above
            private List<vtkVolume> maskvols = new List<vtkVolume>();
            //private List<vtkFixedPointVolumeRayCastMapper> maskmappers = new List<vtkFixedPointVolumeRayCastMapper>();
            List<vtkSmartVolumeMapper> maskmappers = new List<vtkSmartVolumeMapper>();
            private List<vtkColorTransferFunction> maskctfs = new List<vtkColorTransferFunction>();
            private List<vtkPiecewiseFunction> maskspwfs = new List<vtkPiecewiseFunction>();            

            /// <summary>
            /// Renderer object.
            /// </summary>
            public vtkRenderer renderer = vtkRenderer.New();

            /// <summary>
            /// Method for initializing components.
            /// </summary>
            public void Initialize()
            {                
                //Initialize new volume components
                vol = vtkVolume.New();

                mapper = vtkFixedPointVolumeRayCastMapper.New();
                mapper.DebugOn();
                mapper.ReleaseDataFlagOn();
                //mapper = vtkGPUVolumeRayCastMapper.New();
                //mapper = vtkOpenGLGPUVolumeRayCastMapper.New();
                //mapper.SetMaxMemoryInBytes((long)5E9);

                GPUmapper = vtkSmartVolumeMapper.New();
                GPUmapper.SetMaxMemoryInBytes((long)Math.Pow(2, 31));  // 2GB limit

                ctf = vtkColorTransferFunction.New();
                spwf = vtkPiecewiseFunction.New();
                renderer = vtkRenderer.New();
            }

            /// <summary>
            /// Method for initializing mask components
            /// </summary>
            /// <param name="N"></param>
            public void InitializeMasks(int N)
            {
                maskvols = new List<vtkVolume>();
                maskmappers = new List<vtkSmartVolumeMapper>();
                maskctfs = new List<vtkColorTransferFunction>();
                maskspwfs = new List<vtkPiecewiseFunction>();
                for (int k = 0; k< N; k++)
                {                    
                    //Initialize mask
                    maskvols.Add(vtkVolume.New());
                    //maskmapper = vtkSmartVolumeMapper.New();
                    vtkSmartVolumeMapper tmpmapper = vtkSmartVolumeMapper.New();
                    tmpmapper.SetMaxMemoryInBytes((long)Math.Pow(2, 31));
                    maskmappers.Add(tmpmapper);
                    maskctfs.Add(vtkColorTransferFunction.New());
                    maskspwfs.Add(vtkPiecewiseFunction.New());
                }                
            }

            /// <summary>
            /// Method for disposing mask components, useful for memory management
            /// </summary>
            public void DisposeMasks()
            {
                //Dispose mask components
                for (int k = 0; k < maskvols.Count; k++)
                {
                    //Remove volume from mask
                    renderer.RemoveVolume(maskvols.ElementAt(k));
                    maskvols.ElementAt(k).Dispose();
                    maskmappers.ElementAt(k).Dispose();
                    maskctfs.ElementAt(k).Dispose();
                    maskspwfs.ElementAt(k).Dispose();
                }                    
            }

            /// <summary>
            /// Method for updating volume color.
            /// </summary>
            /// <param name="cmin"></param>
            /// <param name="cmax"></param>
            public void setColor(int cmin, int cmax)
            {
                /*Takes gray value range as input arguments*/
                //Clear ctf
                ctf.Dispose();
                ctf = vtkColorTransferFunction.New();
                //New range for gray values
                ctf.AddRGBPoint(cmin, 0, 0, 0);
                ctf.AddRGBPoint(cmax, 0.8, 0.8, 0.8);
                //Update volume color
                vol.GetProperty().SetColor(ctf);
                vol.Update();
            }

            /// <summary>
            /// Method for connecting volume rendering components.
            /// </summary>
            /// <param name="input">Volume data input.</param>
            /// <param name="inputRenderer">Renderer object.</param>
            /// <param name="cmin">Grayscale minimum.</param>
            /// <param name="cmax">Grayscale maximum.</param>
            public void connectComponents(vtkImageData input, vtkRenderer inputRenderer, int cmin, int cmax)
            {
                /*Arguments: volumetric data and renderer*/

                //Set renderer
                renderer = inputRenderer;
                //Mapper
                if (large)
                {
                    mapper.SetInput(input);
                    mapper.Update();
                }
                else
                {
                    GPUmapper.SetInput(input);
                    GPUmapper.Update();
                }
                //Color
                ctf.AddRGBPoint(cmin, 0.0, 0.0, 0.0);
                ctf.AddRGBPoint(cmax, 0.8, 0.8, 0.8);
                //Opacity, background in microCT data is < 70
                spwf.AddPoint(0, 0);
                spwf.AddPoint(70, 0.0);
                spwf.AddPoint(80, 0.3);
                spwf.AddPoint(150, 0.4);
                spwf.AddPoint(255, 0.5);
                //Volume parameters
                vol.GetProperty().SetColor(ctf);
                vol.GetProperty().SetScalarOpacity(spwf);
                if (large)
                    vol.SetMapper(mapper);
                else
                    vol.SetMapper(GPUmapper);
                vol.Update();
                //Renderer back ground
                renderer.GradientBackgroundOn();
                renderer.SetBackground(0.0, 0.0, 0.0);
                renderer.SetBackground2(1.0, 1.0, 1.0);
                renderer.AddVolume(vol);                
                //Set Camera
                renderer.GetActiveCamera().SetPosition(0.5, 1, 0);
                renderer.GetActiveCamera().SetFocalPoint(0, 0, 0);
                renderer.GetActiveCamera().SetViewUp(0, 0, 1);
                renderer.GetActiveCamera().SetEyeAngle(10);
                renderer.ResetCamera();
            }

            /// <summary>
            /// Method for connecting mask components.
            /// </summary>
            /// <param name="masks"></param>
            /// <param name="colors"></param>
            /// <param name="cmin"></param>
            /// <param name="cmax"></param>
            public void connectMask(List<vtkImageData> masks, List<double[]> colors, int cmin, int cmax)
            {
                DisposeMasks();
                InitializeMasks(masks.Count());
                Console.WriteLine("Number of mappers: {0}",maskmappers.Count());
                Console.WriteLine("Number of masks: {0}", masks.Count());
                for (int k = 0; k < masks.Count(); k++)
                {                    
                    double[] color = colors.ElementAt(k);
                    /*Takes bone mask data as input*/
                    //Mapper
                    maskmappers.ElementAt(k).SetInput(masks.ElementAt(k));
                    maskmappers.ElementAt(k).Update();
                    //Color
                    maskctfs.ElementAt(k).AddRGBPoint(cmin, 0, 0, 0);
                    maskctfs.ElementAt(k).AddRGBPoint(cmax, color[0], color[1], color[2]);
                    //Opacity, background in microCT data is < 70
                    maskspwfs.ElementAt(k).AddPoint(0, 0);
                    maskspwfs.ElementAt(k).AddPoint(70, 0.0);
                    maskspwfs.ElementAt(k).AddPoint(80, 0.6);
                    maskspwfs.ElementAt(k).AddPoint(150, 0.8);
                    maskspwfs.ElementAt(k).AddPoint(255, 0.85);
                    //
                    //Volume parameters
                    maskvols.ElementAt(k).GetProperty().SetColor(maskctfs.ElementAt(k));
                    maskvols.ElementAt(k).GetProperty().SetScalarOpacity(maskspwfs.ElementAt(k));
                    maskvols.ElementAt(k).SetMapper(maskmappers.ElementAt(k));
                    maskvols.ElementAt(k).Update();
                    //Renderer back ground
                    renderer.AddVolume(maskvols.ElementAt(k));
                }
            }

            /// <summary>
            /// Methods for disposing the object
            /// </summary>
            public void Dispose()
            {
                DisposeMasks();
                //Clear the pipeline
                renderer.RemoveVolume(vol);
                //Dispose volume components
                vol.Dispose();
                mapper.Dispose();
                ctf.Dispose();
                spwf.Dispose();
                renderer.Dispose();
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            /// <param name="disposing"></param>
            protected virtual void Dispose(bool disposing)
            {
                Disposed = true;
            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            protected bool Disposed { get; private set; }
        }

        /// <summary>
        /// Pipeline for image rendering. Contains methods for connecting components
        /// and memory management.
        /// </summary>
        public class imagePipeLine : IDisposable
        {
            //Image variables
            private vtkImageActor actor = vtkImageActor.New();
            private vtkLookupTable colorTable = vtkLookupTable.New();
            private vtkImageMapToColors colorMapper = vtkImageMapToColors.New();

            //Bone mask variables
            private List<vtkImageActor> maskactors = new List<vtkImageActor>();
            private List<vtkLookupTable> maskcolorTables = new List<vtkLookupTable>();
            private List<vtkImageMapToColors> maskcolorMappers = new List<vtkImageMapToColors>();

            /// <summary>
            /// Renderer object.
            /// </summary>
            public vtkRenderer renderer = vtkRenderer.New();

            /// <summary>
            /// Initialize components, similar as in volume pipeline
            /// </summary>
            public void Initialize()
            {
                actor = vtkImageActor.New();
                colorTable = vtkLookupTable.New();
                colorMapper = vtkImageMapToColors.New();
            }

            /// <summary>
            /// Initialize mask components
            /// </summary>
            public void InitializeMask(int N)
            {
                //New lists
                maskactors = new List<vtkImageActor>();
                maskcolorTables = new List<vtkLookupTable>();
                maskcolorMappers = new List<vtkImageMapToColors>();
                for (int k = 0; k < N; k++)
                {
                    maskactors.Add(vtkImageActor.New());
                    maskcolorTables.Add(vtkLookupTable.New());
                    maskcolorMappers.Add(vtkImageMapToColors.New());
                }
            }

            /// <summary>
            /// Dispose mask components, memory management
            /// </summary>
            public void DisposeMasks()
            {
                for(int k = 0; k<maskactors.Count(); k++)
                {
                    renderer.RemoveActor(maskactors.ElementAt(0));
                    maskactors.ElementAt(k).Dispose();
                    maskcolorTables.ElementAt(k).Dispose();
                    maskcolorMappers.ElementAt(k).Dispose();
                }                
            }

            /// <summary>
            /// Method for setting color table.
            /// Creates lookup table for grayvalues.
            /// </summary>
            /// <param name="cmin">Lower limit for color table.</param>
            /// <param name="cmax">Upper limit for color table.</param>
            public void setGrayLevel(int cmin, int cmax)
            {
                //Set lookup table range
                colorTable.SetTableRange(cmin, cmax);
                //Loop over range and add points to the table
                for (int cvalue = 0; cvalue <= 255; cvalue++)
                {
                    //Current int value / max value
                    double val = (double)cvalue / (double)cmax;
                    //Values below maximum are set to appropriate value
                    if (val < 1.0 && cvalue >= cmin)
                    {
                        colorTable.SetTableValue(cvalue, val, val, val, 1);
                    }
                    if (val < 1.0 && cvalue < cmin)
                    {
                        colorTable.SetTableValue(cvalue, 0, 0, 0, 1);
                    }
                    //Values over maximum are set to 1
                    if (val >= 1 && cvalue >= cmin)
                    {
                        colorTable.SetTableValue(cvalue, 1.0, 1.0, 1.0, 1);
                    }
                }
                //Build the table
                colorTable.Build();
                //Attach to color mapper
                colorMapper.SetLookupTable(colorTable);
            }

            /// <summary>
            /// Method for setting mask color table.
            /// Creates lookup table for grayvalues.
            /// </summary>
            /// <param name="idx"></param>
            /// <param name="cmin">Lower limit for color table.</param>
            /// <param name="cmax">Upper limit for color table.</param>
            /// <param name="color"></param>
            public void setMaskGrayLevel(int idx, int cmin, int cmax, double[] color)
            {
                //Set lookup table range
                maskcolorTables.ElementAt(idx).SetTableRange(cmin, cmax);
                //Loop over range and add points to the table
                for (int cvalue = 0; cvalue <= 255; cvalue++)
                {
                    //Current int value / max value
                    double val = (double)cvalue / (double)cmax;
                    //Values below maximum are set to appropriate value
                    if (val < 1.0 && cvalue > cmin)
                    {
                        maskcolorTables.ElementAt(idx).SetTableValue(cvalue, color[0]*val, color[1] * val, color[2] * val, 0.9);
                    }
                    if (val < 1.0 && cvalue <= cmin)
                    {
                        maskcolorTables.ElementAt(idx).SetTableValue(cvalue, 0, 0, 0, 0);
                    }
                    //Values over maximum are set to 1
                    if (val >= 1 && cvalue > cmin)
                    {
                        maskcolorTables.ElementAt(idx).SetTableValue(cvalue, color[0], color[1], color[2], 0.9);
                    }
                }
                //Build the table
                maskcolorTables.ElementAt(idx).Build();
                //Attach to color mapper
                maskcolorMappers.ElementAt(idx).SetLookupTable(maskcolorTables.ElementAt(idx));

            }

            /// <summary>
            /// Method for connecting image components
            /// </summary>
            /// <param name="I"></param>
            /// <param name="inputRenderer"></param>
            /// <param name="cmin"></param>
            /// <param name="cmax"></param>
            public void connectComponents(vtkImageData I, vtkRenderer inputRenderer, int cmin, int cmax)
            {
                /*Arguments: input image, renderer, color range*/

                //Set renderer
                renderer = inputRenderer;
                //Set color
                setGrayLevel(cmin, cmax);
                colorMapper.SetInput(I);
                colorMapper.Update();
                //Set mapper
                actor.SetInput(colorMapper.GetOutput());
                //Connect to renderer
                renderer.AddActor(actor);
                renderer.SetBackground(0.0, 0.0, 0.0);
            }

            /// <summary>
            /// Method for connecting mask components
            /// </summary>
            /// <param name="masks">Mask image data.</param>
            /// <param name="colors"></param>
            /// <param name="cmin"></param>
            /// <param name="cmax"></param>
            public void connectMask(List<vtkImageData> masks, List<double[]> colors, int cmin, int cmax)
            {
                for (int k = 0; k < masks.Count(); k++)
                {
                    //Add components

                    //Set mask color and mapper
                    setMaskGrayLevel(k,cmin, cmax, colors.ElementAt(k));
                    maskcolorMappers.ElementAt(k).SetInput(masks.ElementAt(k));
                    maskcolorMappers.ElementAt(k).Update();                    
                    //Connect mapper
                    maskactors.ElementAt(k).SetInput(maskcolorMappers.ElementAt(k).GetOutput());
                    //Connect to renderer
                    renderer.AddActor(maskactors.ElementAt(k));
                }
            }

            /// <summary>
            /// Methods for disposing the object
            /// </summary>
            public void Dispose()
            {
                DisposeMasks();
                //Clear the pipeline
                renderer.RemoveActor(actor);
                //Dispose volume components
                actor.Dispose();
                colorMapper.Dispose();
                colorTable.Dispose();                
                renderer.Dispose();
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            /// <param name="disposing"></param>
            protected virtual void Dispose(bool disposing)
            {
                Disposed = true;
            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            protected bool Disposed { get; private set; }
        }

        /// <summary>
        /// Class for rendering images and 3D volumes. Volume and image pipelines defined above are called,
        /// depending on the input.
        /// </summary>
        public class renderPipeLine : IDisposable
        {
            //Declarations

            //Renderwindow
            vtkRenderWindow renWin;
            //Renderer
            vtkRenderer renderer = vtkRenderer.New();
            //Interactor
            Interactors interactor;

            /// <summary>
            /// Original loaded image data as vtkImageData object.
            /// </summary>
            public vtkImageData idata = vtkImageData.New();
            List<vtkImageData> imasks = new List<vtkImageData>();
            List<double[]> maskcolors = new List<double[]>();

            //Rendering pipelines
            volumePipeLine volPipe = new volumePipeLine();
            imagePipeLine imPipe = new imagePipeLine();

            //Current slice and gray values
            int[] sliceN = new int[3] { 0, 0, 0 };
            int curAx = 0;
            int[] gray = new int[2] { 0, 255 };
                       

            //flags for components
            int has_volume = 0;
            int has_image = 0;
            int has_mask = 0;

            //Methods for communicating with GUI

            /// <summary>
            /// Connect renderwindow.
            /// </summary>
            /// <param name="input"></param>
            public void connectWindow(vtkRenderWindow input)
            {
                renWin = input;
            }

            /// <summary>
            /// Connect input volume.
            /// </summary>
            /// <param name="input">Data input to be connected.</param>
            public void connectData(string input)
            {
                idata.Dispose();
                vtkImageData tmp = vtkImageData.New();
                tmp = Functions.loadVTK(input);
                idata = vtkImageData.New();
                idata.DeepCopy(tmp);
                tmp.Dispose();

                for(int k = 0; k<imasks.Count(); k++)
                {
                    imasks.ElementAt(k).Dispose();
                }                
            }
            
            /// <summary>
            /// Connect input volume.
            /// </summary>
            /// <param name="input">Data input to be connected.</param>
            public void connectDataFromMemory(vtkImageData input)
            {
                idata = input;
            }

            /// <summary>
            /// Connect CT stack from memory
            /// </summary>
            /// <param name="input_data">Bone mask input to be connected.</param>
            public void connectDataFromData(vtkImageData input_data)
            {
                idata = input_data;
            }

            /// <summary>
            /// Connect bone mask.
            /// </summary>
            /// <param name="input">Bone mask input to be connected.</param>
            public void connectMask(string input)
            {
                //Clear current masks
                for(int k = 0; k<imasks.Count(); k++)
                {
                    imasks.ElementAt(0).Dispose();                    
                }
                
                imasks = new List<vtkImageData>();
                maskcolors = new List<double[]>();
                GC.Collect();
                //Load mask
                vtkImageData tmp = Functions.loadVTK(input);
                //Set graylevel
                vtkImageMathematics math = vtkImageMathematics.New();
                math.SetInput1(idata);
                math.SetInput2(tmp);
                math.SetOperationToMultiply();
                math.Update();
                //Add new mask to list
                imasks.Add(math.GetOutput());
                //Generate colors                
                maskcolors.Add(new double[] { 253 / 255, 192 / 255, 134 / 255 });

                has_mask = 1;
            }

            /// <summary>
            /// Connect bone mask from memory.
            /// </summary>
            /// <param name="input_masks">Bone mask input to be connected.</param>
            /// <param name="multiply">Selection to multiply mask and data.</param>
            public void connectMaskFromData(List<vtkImageData> input_masks, int multiply = 1)
            {
                //Add masks
                foreach (vtkImageData input_mask in input_masks)
                {
                    if (multiply == 1)
                    {
                        //Multiply data with input mask
                        vtkImageMathematics math = vtkImageMathematics.New();
                        math.SetInput1(idata);
                        math.SetInput2(input_mask);
                        math.SetOperationToMultiply();
                        math.SetNumberOfThreads(24);
                        math.Update();
                        //Set mask extent to match with the data extent
                        int[] dims = idata.GetExtent();
                        vtkImageReslice reslice = vtkImageReslice.New();
                        reslice.SetInputConnection(math.GetOutputPort());
                        reslice.SetOutputExtent(dims[0], dims[1], dims[2], dims[3], dims[4], dims[5]);
                        reslice.Update();
                        vtkImageData mask = vtkImageData.New();
                        mask.DeepCopy(reslice.GetOutput());
                        imasks.Add(mask);
                        reslice.Dispose();
                        math.Dispose();
                        input_mask.Dispose();
                    }
                    if (multiply == 0)
                    {
                        imasks.Add(input_mask);
                    }
                }

                //Add colors
                maskcolors = new List<double[]>();
                //for (int c = 0; c < input_masks.Count(); c++)
                //{
                //    double val = (c / 3 + 1) * 0.25;
                //    double[] cur = new double[3];
                //    for (int k = 0; k < 3; k++)
                //    {
                //        cur[k] += val;
                //    }
                //    cur[c % 3] = 0.8;
                //    maskcolors.Add(cur);
                //}
                for (int c = 0; c < input_masks.Count(); c++)
                {
                    double[] cur = new double[3];
                    if (c == 0)
                    {
                        //cur = new double[3]{ 217 / 255.0, 95 / 255.0, 2 / 255.0 };
                        cur = new double[3] { 240 / 255.0, 126 / 255.0, 49 / 255.0 };
                    }
                    else if (c == 1)
                    {
                        //cur = new double[3] { 27 / 255.0, 158 / 255.0, 119 / 255.0 };
                        cur = new double[3] { 128 / 255.0, 160 / 255.0, 60 / 255.0 };
                    }
                    else if (c == 2)
                    {
                        //cur = new double[3] { 117 / 255.0, 112 / 255.0, 179 / 255.0 };
                        cur = new double[3] { 132 / 255.0, 102 / 255.0, 179 / 255.0 };
                    }
                    maskcolors.Add(cur);
                }
                //maskcolors.Add(new double[3] { 253 / 255, 192 / 255, 134 / 255 });
                //maskcolors.Add(new double[3] { 190 / 255, 174 / 255, 212 / 255 });
                //maskcolors.Add(new double[3] { 127 / 255, 201 / 255, 127 / 255 });

                has_mask = 1;
            }

            /// <summary>
            /// Update slice.
            /// </summary>
            /// <param name="inpSlice">Slice number.</param>
            /// <param name="inpAx">Current axis.</param>
            /// <param name="inpGray">Gray values.</param>
            public void updateCurrent(int[] inpSlice, int inpAx, int[] inpGray)
            {
                sliceN = inpSlice;
                curAx = inpAx;
                gray = inpGray;
            }

            /// <summary>
            /// 3D volume rendering.
            /// </summary>
            public bool renderVolume()
            {
                // Path for error report
                string errorPath =
                    new DirectoryInfo(Directory.GetCurrentDirectory()) // Get current directory
                    .Parent.Parent.Parent.Parent.FullName // Move to repository root
                    + ".\\Default\\errors.txt";

                // Initialize
                var fow = vtkFileOutputWindow.New();
                fow.SetFileName(errorPath);
                vtkOutputWindow.SetInstance(fow);
                
                // Detach first renderer from render window. Prevents multiple images from being
                // rendered on top of each other, and helps with memory management.
                renWin.RemoveRenderer(renWin.GetRenderers().GetFirstRenderer());

                // Initialize new volume rendering pipeline and connect components
                // Disposes existing pipeline and initializes new pipeline
                if(has_volume == 1)
                {
                    volPipe.Dispose();
                    interactor.Dispose();
                }
                if(has_image == 1)
                {
                    imPipe.Dispose();
                    imPipe.Initialize();
                    interactor.Dispose();
                }

                // Check input Volume size
                //Get input extent
                int[] dims = idata.GetExtent();

                //Compute dimensions
                int h = dims[1] - dims[0] + 1;
                int w = dims[3] - dims[2] + 1;
                int d = dims[5] - dims[4] + 1;
                volPipe.large = (long)h * w * d >= (long)Math.Pow(2, 31);

                // Initialize pipeline
                volPipe.Initialize();

                // Initialize renderer, dispose existing renderer and connect new renderer
                renderer.Dispose();
                renderer = vtkRenderer.New();


                // Connect input data and renderer to rendering pipeline
                volPipe.connectComponents(idata, renderer, gray[0], gray[1]);

                // Connect renderer to render window
                renWin.AddRenderer(renderer);

                // Update flags
                has_volume = 1;
                has_image = 0;

                // Render small volume
                if (!volPipe.large)
                    renWin.Render();

                // Update interactor
                interactor = new Interactors(renWin);
                interactor.set_default();

                return volPipe.large;
            }       

            /// <summary>
            /// 2D Image rendering.
            /// </summary>
            public void renderImage()
            {
                // Detach first renderer from render window. Prevents multiple images from being
                // rendered on top of each other, and helps with memory management.
                renWin.RemoveRenderer(renWin.GetRenderers().GetFirstRenderer());

                // Initialize new image rendering pipeline and connect components
                // Disposes existing pipeline and initializes new pipeline
                // Initialize new volume rendering pipeline and connect components
                // Disposes existing pipeline and initializes new pipeline
                if (has_volume == 1)
                {
                    volPipe.Dispose();
                    interactor.Dispose();
                }
                if (has_image == 1)
                {
                    imPipe.Dispose();
                    interactor.Dispose();
                }

                imPipe.Initialize();

                vtkImageData slice = Functions.volumeSlicer(idata, sliceN, curAx);
                                
                // Initialize renderer
                renderer.Dispose();
                renderer = vtkRenderer.New();

                // Connect components to rendering pipeline
                imPipe.connectComponents(slice, renderer, gray[0], gray[1]);
                // Connect renderer to render window
                renWin.AddRenderer(renderer);                

                // Update flags
                has_image = 1;
                has_volume = 0;

                // Render image
                renWin.Render();

                // Update interactor
                interactor = new Interactors(renWin);
                if (curAx == 2) { interactor.set_2d_nodraw(); }
                else { interactor.set_2d(); }
                interactor.get_camera();
                interactor.set_slice(sliceN[curAx]);
            }

            /// <summary>
            /// Render 3D bone mask, follows similar pipeline as regular volume rendering.
            /// Doesn't require new renderers to be initialized/connected
            /// </summary>
            public void renderVolumeMask()
            {
                volPipe.DisposeMasks();
                volPipe.InitializeMasks(imasks.Count());

                // Initialize new volume rendering and connect components
                volPipe.connectMask(imasks,maskcolors,gray[0],gray[1]);
                // Render volume
                renWin.Render();
            }

            /// <summary>
            /// Render image mask, see <seealso cref="renderVolumeMask()"/>.
            /// </summary>
            public void renderImageMask()
            {
                // Connect 2D mask to image rendering pipeline
                imPipe.DisposeMasks();
                imPipe.InitializeMask(imasks.Count());

                // Get mask slice
                List<vtkImageData> maskSlices = new List<vtkImageData>();
                foreach(vtkImageData mask in imasks)
                {
                    vtkImageData maskSlice = Functions.volumeSlicer(mask, sliceN, curAx);
                    maskSlices.Add(maskSlice);
                }
                
                imPipe.connectMask(maskSlices,maskcolors,gray[0],gray[1]);

                // Render image
                renWin.Render();
            }

            /// <summary>
            /// Set color for volume.
            /// </summary>
            public void setVolumeColor()
            {
                volPipe.setColor(gray[0], gray[1]);
                if (!volPipe.large)
                    renWin.Render();
            }

            /// <summary>
            /// Reset camera.
            /// </summary>
            public void resetCamera()
            {
                renderer.GetActiveCamera().SetPosition(0.5, 1, 0);
                renderer.GetActiveCamera().SetFocalPoint(0, 0, 0);
                renderer.GetActiveCamera().SetViewUp(0, 0, 1);
                renderer.ResetCamera();
                if (!volPipe.large)
                    renWin.Render();
            }

            /// <summary>
            /// Remove bone mask
            /// </summary>
            public void removeMask()
            {
                if (has_volume == 1)
                {
                    volPipe.DisposeMasks();
                }
                if (has_image == 1)
                {
                    imPipe.DisposeMasks();
                }

                for (int k = 0; k < imasks.Count(); k++)
                {
                    imasks.ElementAt(k).Dispose();
                }
                imasks = new List<vtkImageData>();
                has_mask = 0;
            }
            
            /// <summary>
            /// Get data dimensions
            /// </summary>
            /// <returns>Data dimensions.</returns>
            public int[] getDims()
            {
                int[] dims = idata.GetExtent();
                return dims;
            }

            /// <summary>
            /// Get VOI from the data
            /// </summary>
            /// <returns> VOI</returns>
            public vtkImageData getVOI(int[] extent = null, int[] orientation = null)
            {
                // Empty output data
                vtkImageData voi = vtkImageData.New();

                // If no VOI is specified, full data is returned
                if (extent == null)
                {
                    voi = idata;
                }
                else
                {
                    // Extract VOI
                    vtkExtractVOI extractor = vtkExtractVOI.New();
                    extractor.SetInput(idata);
                    extractor.SetVOI(extent[0], extent[1], extent[2], extent[3], extent[4], extent[5]);
                    extractor.Update();
                    voi.DeepCopy(extractor.GetOutput());
                    extractor.Dispose();
                }
                // If order of the axes is specified, the return array is permuted
                if (orientation != null)
                {
                    vtkImagePermute permuter = vtkImagePermute.New();
                    permuter.SetInput(voi);
                    permuter.SetFilteredAxes(orientation[0], orientation[1], orientation[2]);
                    permuter.Update();
                    voi.DeepCopy(permuter.GetOutput());
                    permuter.Dispose();
                }

                return voi;
            }

            /// <summary>
            /// Get VOI from the data
            /// </summary>
            /// <returns> VOI</returns>
            public vtkImageData getMaskVOI(int idx, int[] extent = null, int[] orientation = null)
            {
                // Empty output data
                vtkImageData voi = vtkImageData.New();

                // If no VOI is specified, full data is returned
                if (extent == null)
                {
                    voi = imasks.ElementAt(idx);
                }
                else
                {
                    // Extract VOI
                    vtkExtractVOI extractor = vtkExtractVOI.New();
                    extractor.SetInput(imasks.ElementAt(idx));
                    extractor.SetVOI(extent[0], extent[1], extent[2], extent[3], extent[4], extent[5]);
                    extractor.Update();
                    voi.DeepCopy(extractor.GetOutput());
                    extractor.Dispose();
                }
                // If order of the axes is specified, the return array is permuted
                if (orientation != null)
                {
                    vtkImagePermute permuter = vtkImagePermute.New();
                    permuter.SetInput(voi);
                    permuter.SetFilteredAxes(orientation[0], orientation[1], orientation[2]);
                    permuter.Update();
                    voi.DeepCopy(permuter.GetOutput());
                    permuter.Dispose();
                }
                return voi;
            }

            /// <summary>
            /// Method for automatic sample rotation. Displays angles on Mainform.
            /// </summary>
            /// <returns></returns>
            public string auto_rotate()
            {
                vtkImageData tmp = vtkImageData.New();
                tmp.DeepCopy(idata);                
                tmp = Functions.auto_rotate(tmp, out double[] angles, 3);
                idata = vtkImageData.New();
                idata.DeepCopy(tmp);
                tmp.Dispose();
                GC.Collect();

                var angletext = "Rotation angles: " + angles[0].ToString("#0.##", CultureInfo.InvariantCulture) + " | " + angles[1].ToString("#0.##", CultureInfo.InvariantCulture);
                return angletext;
            }

            /// <summary>
            /// Method for cropping sample edges.
            /// </summary>
            /// <param name="size"></param>
            /// <param name="get_center"></param>
            public void center_crop(int size = 400,bool get_center = false)
            {
                vtkImageData tmp = vtkImageData.New();
                tmp.DeepCopy(idata);
                idata.Dispose();
                tmp = Processing.center_crop(tmp,size,get_center);                
                idata = vtkImageData.New();
                idata.DeepCopy(tmp);
                tmp.Dispose();

                if(has_mask == 1)
                {
                    List<vtkImageData> tmplist = new List<vtkImageData>();
                    for(int k = 0; k<imasks.Count(); k++)
                    {
                        vtkImageData tmpmask = vtkImageData.New();
                        tmpmask.DeepCopy(imasks.ElementAt(k));
                        imasks.ElementAt(k).Dispose();
                        tmpmask = Processing.center_crop(tmpmask, size, get_center);
                        tmplist.Add(tmpmask);
                    }
                    imasks = new List<vtkImageData>();
                    imasks = tmplist;
                }
            }

            /// <summary>
            /// Pipeline for bone-cartilage interface segmentation.
            /// </summary>
            public void segmentation(string modelPath)
            {
                // Console output
                Console.WriteLine("Calculating mask using UNet architecture...");

                // Get sample dimensions
                int[] extent = idata.GetExtent();

                // Check the sample height for segmentation region
                int sample_height = extent[5] - extent[4] - 1;

                int[] voi_extent = new int[6];
                int[] batch_dims = new int[3];

                /*
                if (sample_height < 1000)                                
                {
                    voi_extent = new int[] { extent[0], extent[1], extent[2], extent[3], extent[4], extent[4] + 383 };
                    batch_dims = new int[] { 384, 448, 1 };
                }                
                if (sample_height >= 1000 && sample_height <= 1600)                
                {
                    voi_extent = new int[] { extent[0], extent[1], extent[2], extent[3], extent[4] + 20, extent[4] + 20 + 447 };
                    batch_dims = new int[] { 448, 448, 1 };
                }
                if (sample_height > 1600)                
                {
                    voi_extent = new int[] { extent[0], extent[1], extent[2], extent[3], extent[4] + 50, extent[4] + 50 + 511 };
                    batch_dims = new int[] { 512, 448, 1 };
                }
                */

                // Pipeline variables
                voi_extent = new int[] { extent[0], extent[1], extent[2], extent[3], extent[4], extent[4]+767 };
                batch_dims = new int[] { 448, 768, 1 };
                int batchSize = 28;
                int[] segmentationAxes = new int[] { 0, 1 };

                // Empty list for output
                List<vtkImageData> outputs;

                IO.segmentation_pipeline(out outputs, this, batch_dims, voi_extent, segmentationAxes, modelPath, batchSize);

                vtkImageThreshold t = vtkImageThreshold.New();
                if (outputs.Count() == 2)
                {
                    // Sum operation
                    vtkImageMathematics math = vtkImageMathematics.New();
                    math.SetOperationToAdd();

                    // Weighting
                    vtkImageMathematics _tmp1 = vtkImageMathematics.New();
                    _tmp1.SetInput1(outputs.ElementAt(0));
                    _tmp1.SetConstantK(0.5);
                    _tmp1.SetOperationToMultiplyByK();
                    _tmp1.Update();

                    vtkImageMathematics _tmp2 = vtkImageMathematics.New();
                    _tmp2.SetInput1(outputs.ElementAt(1));
                    _tmp2.SetConstantK(0.5);
                    _tmp2.SetOperationToMultiplyByK();
                    _tmp2.Update();

                    math.SetInput1(_tmp1.GetOutput());
                    math.SetInput2(_tmp2.GetOutput());

                    math.Update();

                    // Threshold
                    t = vtkImageThreshold.New();
                    t.SetInputConnection(math.GetOutputPort());
                    t.ThresholdByUpper(0.5 * 255.0);
                    t.SetOutValue(0);
                    t.SetInValue(255.0);
                    t.Update();
                }
                else
                {
                    // Threshold
                    t = vtkImageThreshold.New();
                    t.SetInput(outputs.ElementAt(0));
                    t.ThresholdByUpper(0.5 * 255.0);
                    t.SetOutValue(0);
                    t.SetInValue(255.0);
                    t.Update();
                }

                List<vtkImageData> tmpmask = new List<vtkImageData>();
                tmpmask.Add(vtkImageData.New());
                tmpmask.ElementAt(0).DeepCopy(t.GetOutput());
                t.Dispose();
                connectMaskFromData(tmpmask, 1);

                // Console output
                Console.WriteLine("Segmentation done");
            }

            /// <summary>
            /// Pipeline for extracting analysis volumes-of-interest.
            /// </summary>
            public void analysis_vois()
            {
                center_crop(400, true);
                vtkImageData ccartilage, dcartilage, scartilage;
                Functions.get_analysis_vois(out dcartilage, out ccartilage, out scartilage, idata, imasks.ElementAt(0));

                removeMask();

                connectMaskFromData(new List<vtkImageData> { ccartilage, dcartilage, scartilage}, 0);
            }

            /// <summary>
            /// Pipeline for surface artefact removal.
            /// </summary>
            /// <param name="rendermask"></param>
            public void remove_artefact(bool rendermask = false)
            {

                //Get line ends as world coordinates
                double[] points = interactor.get_position();
                //Create new imagedata for cropping
                vtkImageData tmp = vtkImageData.New();
                tmp.DeepCopy(idata);
                idata.Dispose();
                //Remove artefacts
                tmp = Processing.remove_artefacts(tmp, points, curAx);
                //Return to original image data variable
                idata = vtkImageData.New();
                idata.DeepCopy(tmp);
                //Memory management
                tmp.Dispose();
            }

            /// <summary>
            /// Saves sample data including processing steps.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="path"></param>
            public void save_data(string name, string path)
            {
                //Functions.saveVTKPNG(idata, path, name);
                Functions.saveVTK(idata, path, name);
            }

            /// <summary>
            /// Saves calculated masks.
            /// </summary>
            /// <param name="names"></param>
            /// <param name="path"></param>
            public void save_masks(string[] names, string path)
            {
                for(int k = 0; k<names.Length; k++)
                {
                    //Functions.saveVTKPNG(imasks.ElementAt(k), path, names[k]);
                    Functions.saveVTK(imasks.ElementAt(k), path, names[k]);
                }
            }

            /// <summary>
            /// Pipeline for calculating mean, standard deviation images and grading the VOI.
            /// </summary>
            /// <param name="models">Paths to grading models.</param>
            /// <param name="parameters">Paths to LBP parameters.</param>
            /// <param name="sample_name">Name of the sample.</param>
            /// <returns>OA grade for calculated VOIs.</returns>
            public string grade_vois(string[] models, string[] parameters, string sample_name)
            {
                string cc_grade = "", deep_grade = "", surf_grade = "";

                //Grade extracted vois, order is calcified, deep, surface
                for(int k = 0; k<imasks.Count(); k++)
                {
                    if(k==0)
                    {
                        double[,] mu; double[,] sd;
                        Processing.get_mean_sd(out mu, out sd, imasks.ElementAt(k), 50);
                        //Processing.MeanAndStd(imasks.ElementAt(k), out double[,] mu, out double[,] sd);
                        Console.WriteLine("Calcified cartilage grade:");
                        cc_grade = "  calcified " + Grading.grade_voi("Calcified zone",sample_name+"_calc", mu, sd, models.ElementAt(k), parameters.ElementAt(k));
                    }
                    else if(k==1)
                    {
                        double[,] mu; double[,] sd;
                        Processing.get_mean_sd(out mu, out sd, imasks.ElementAt(k));
                        //Processing.MeanAndStd(imasks.ElementAt(k), out double[,] mu, out double[,] sd);
                        Console.WriteLine("Deep cartilage grade:");
                        deep_grade = "  deep " + Grading.grade_voi("Deep zone",sample_name + "_deep", mu, sd, models.ElementAt(k), parameters.ElementAt(k));
                    }
                    else if (k == 2)
                    {
                        double[,] mu; double[,] sd;
                        Processing.get_mean_sd(out mu, out sd, imasks.ElementAt(k), 25);
                        //Processing.MeanAndStd(imasks.ElementAt(k), out double[,] mu, out double[,] sd);
                        Console.WriteLine("Surface grade:");
                        surf_grade = "  surface " + Grading.grade_voi("Surface zone",sample_name + "_surf", mu, sd, models.ElementAt(k), parameters.ElementAt(k));
                    }
                }

                return "OA grades: " + surf_grade + deep_grade + cc_grade;
            }

            /// <summary>
            /// Disposing methods and garbage collection.
            /// </summary>
            public void Dispose()
            {
                if(has_volume == 1)
                {
                    volPipe.Dispose();
                }
                if(has_image == 1)
                {
                    imPipe.Dispose();
                }
                idata.Dispose();
                for(int k = 0; k<imasks.Count();k++)
                {
                    imasks.ElementAt(k).Dispose();
                }
                imasks = null;
                idata = null;
                GC.Collect();
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            /// <param name="disposing"></param>
            protected virtual void Dispose(bool disposing)
            {
                Disposed = true;
            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            protected bool Disposed { get; private set; }
        }

        /// <summary>
        /// Line that is used to select surface artefacts to be cropped.
        /// </summary>
        public class vtkLine : IDisposable
        {
            private static vtkLineSource line;
            private static vtkPolyDataMapper linemapper;
            private static vtkRenderWindow renWin;            
            private static vtkActor actor;

            private static int slice;

            /// <summary>
            /// Initialize object.
            /// </summary>
            /// <param name="window"></param>
            /// <param name="N"></param>
            public vtkLine(vtkRenderWindow window, int N)
            {
                renWin = window;                
                slice = N;
            }

            /// <summary>
            /// Draw the line on connected render window.
            /// </summary>
            /// <param name="points"></param>
            public void draw(double[] points)
            {
                //Create new line
                line = vtkLineSource.New();
                line.SetPoint1(points[0], points[1], 0);
                line.SetPoint2(points[2], points[3], 0);
                line.Update();

                //Render the line
                linemapper = vtkPolyDataMapper.New();
                linemapper.SetInputConnection(line.GetOutputPort());
                actor = vtkActor.New();
                actor.SetMapper(linemapper);
                actor.SetPosition(0, 0, slice + 1);
                actor.GetProperty().SetColor(1, 0, 0);
                actor.GetProperty().SetLineWidth(5);
                renWin.GetRenderers().GetFirstRenderer().AddActor(actor);
                renWin.Render();
            }

            /// <summary>
            /// Update the line position.
            /// </summary>
            /// <param name="points"></param>
            public void update(double[] points)
            {
                //Create new line                
                line.SetPoint1(points[0], points[1], 0);
                line.SetPoint2(points[2], points[3], 0);
                line.Modified();

                //Render the line                
                linemapper.SetInputConnection(line.GetOutputPort());
                linemapper.Modified();
                actor.SetMapper(linemapper);
                actor.Modified();

                renWin.Render();
            }


            /// <summary>
            /// Disposing methods
            /// </summary>
            public void Dispose()
            {                
                actor.Dispose();
                line.Dispose();
                linemapper.Dispose();
                GC.Collect();
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            /// <param name="disposing"></param>
            protected virtual void Dispose(bool disposing)
            {
                Disposed = true;

            }
            /// <summary>
            /// Dispose boolean.
            /// </summary>
            protected bool Disposed { get; private set; }
        }

        /// <summary>
        /// Render input data to new window.
        /// </summary>
        /// <param name="inputData">vtkImageData to be rendered.</param>
        public static void RenderToNewWindow(vtkImageData inputData)
        {
            // Render cropped volume
            var renwin = vtkRenderWindow.New();
            var vol = new renderPipeLine();
            vol.connectWindow(renwin);
            vol.connectDataFromData(inputData);
            vol.renderVolume();
        }
    }
}
