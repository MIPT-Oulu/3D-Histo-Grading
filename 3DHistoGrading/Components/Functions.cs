﻿using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kitware.VTK;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace HistoGrading.Components
{
    /// <summary>
    /// Utility functions that are used in the software.
    /// </summary>
    public class Functions
    {
        /// <summary>
        /// Load image files into vtkImageData.
        /// </summary>
        /// <param name="path">Path to images.</param>
        /// <param name="extension">Image extension.</param>
        /// <returns></returns>
        public static vtkImageData VTKLoader(string path, string extension)
        {   
            /*DEPRECATED!!*/
            //Output
            vtkImageData data = vtkImageData.New();
            //Get files from path
            DirectoryInfo d = new DirectoryInfo(@path);
            FileInfo[] files = d.GetFiles();

            vtkStringArray allfiles = vtkStringArray.New();
            //Iterate over files and read image data
            foreach (FileInfo file in files)
            {
                //Fullfile
                string fullfile = Path.Combine(path, file.Name);
                allfiles.InsertNextValue(fullfile);
            }
            if (extension == ".png")
            {
                vtkPNGReader reader = vtkPNGReader.New();
                reader.SetFileNames(allfiles);
                reader.Update();
                data = reader.GetOutput();
                reader.Dispose();
            }
            if (extension == ".jpg")
            {
                vtkJPEGReader reader = vtkJPEGReader.New();
                reader.SetFileNames(allfiles);
                reader.Update();
                data = reader.GetOutput();
                reader.Dispose();
            }
            if (extension == ".bmp")
            {
                vtkBMPReader reader = vtkBMPReader.New();
                reader.SetFileNames(allfiles);
                reader.Update();
                data = reader.GetOutput();
                reader.Dispose();
            }
            data.SetScalarTypeToUnsignedChar();
            data.Update();
            return data;
        }

        /// <summary>
        /// Gets a 2D slice from the 3D data.
        /// </summary>
        /// <param name="volume">Full 3D data.</param>
        /// <param name="sliceN">Number of slice to be selected.</param>
        /// <param name="axis">Axis on which selection will be made.</param>
        /// <returns></returns>
        public static vtkImageData volumeSlicer(vtkImageData volume, int[] sliceN, int axis)
        {
            //Initialize VOI extractor and permuter.
            //Permuter will correct the orientation of the output image
            vtkExtractVOI slicer = vtkExtractVOI.New();
            vtkImagePermute permuter = vtkImagePermute.New();
            //Connect data to slicer
            slicer.SetInput(volume);
            slicer.Update();

            //Get data dimensions
            int[] dims = slicer.GetOutput().GetExtent();

            //Get slice

            //Coronal plane
            if (axis == 0)
            {
                //Set VOI
                slicer.SetVOI(sliceN[0], sliceN[0], dims[2], dims[3], dims[4], dims[5]);
                slicer.Update();
                //Permute image (not necessary here)
                permuter.SetInputConnection(slicer.GetOutputPort());
                permuter.SetFilteredAxes(1, 2, 0);
                permuter.Update();
            }
            //Sagittal plane
            if (axis == 1)
            {
                //Set VOI
                slicer.SetVOI(dims[0], dims[1], sliceN[1], sliceN[1], dims[4], dims[5]);
                slicer.Update();
                //Permute image
                permuter.SetInputConnection(slicer.GetOutputPort());
                permuter.SetFilteredAxes(0, 2, 1);
                permuter.Update();
            }
            //Transverse plane, XY
            if (axis == 2)
            {
                //Set VOI
                slicer.SetVOI(dims[0], dims[1], dims[2], dims[3], sliceN[2], sliceN[2]);
                slicer.Update();
                //Permute image
                permuter.SetInputConnection(slicer.GetOutputPort());
                permuter.SetFilteredAxes(0, 1, 2);
                permuter.Update();
            }
            //slicer.Update();

            //Return copy of the slice
            return permuter.GetOutput();
        }

        /// <summary>
        /// Create scalar copy of vtkImageData.
        /// </summary>
        /// <param name="data">Input data.</param>
        /// <returns>Copied data.</returns>
        public static vtkImageData scalarCopy(vtkImageData data)
        {
            /*DEPRECATED!!*/
            //Get data extent
            int[] dims = data.GetExtent();
            vtkImageData newdata = vtkImageData.New();
            newdata.SetExtent(dims[0], dims[1], dims[2], dims[3], dims[4], dims[5]);
            for (int h = dims[0]; h <= dims[1]; h++)
            {
                for (int w = dims[2]; w <= dims[3]; w++)
                {
                    for (int d = dims[4]; d <= dims[5]; d++)
                    {
                        double scalar = data.GetScalarComponentAsDouble(h, w, d, 0);
                        newdata.SetScalarComponentFromDouble(h, w, d, 0, scalar);
                    }

                }
            }
            return newdata;
        }

        /// <summary>
        /// Read all images in folder containing file from given path
        /// Uses ParaLoader class to get the data in vtk format.
        /// </summary>
        /// <param name="path">Directory that includes images to be loaded.</param>
        /// <returns></returns>
        public static vtkImageData loadVTK(string path, int rotate = 1)
        {
            //Declare loader
            ParaLoader loader = new ParaLoader();
            //Set input path to loader
            loader.setInput(path);
            //Load data
            loader.Load();
            //Extract data to variable
            vtkImageData data = loader.GetData(rotate);
            return data;
        }

        /// <summary>
        /// Find all files which correspond to selected file.
        /// When reading multiple files, files must start with the same name, which ends in a digit,
        /// as the selected file and have the same extension.
        /// </summary>
        /// <param name="file">Name for the file to be checked.</param>
        /// <returns></returns>
        public static List<string> getFiles(string file)
        {
            //Get file name and extension
            string fileName = Path.GetFileName(file);
            string extension = Path.GetExtension(file);

            //Get the starting string of the file
            //if the files are numbered, the starting string is recorded and used
            //to find all files with similar names

            //Empty string
            string fstart = "";
            //Length of file name
            int nameL = fileName.Length;
            //Loop from the start of the extension to start of the file name
            //and check for numbers
            for (int k = 5; k < nameL; k++)
            {
                char c = fileName[nameL - k];
                if (Char.IsNumber(c) == false)
                {
                    for (int kk = 0; kk < (nameL - k); kk++)
                    {
                        fstart += fileName[kk];
                    }

                    break;
                }
            }

            //Get directory info
            string path = Path.GetDirectoryName(file);
            DirectoryInfo d = new DirectoryInfo(@path);

            //Get all files
            FileInfo[] allFiles = d.GetFiles();

            //Empty list for found files
            List<string> names = new List<string>();

            //Loop over the files and find files corresponding to the selected files
            foreach (FileInfo curFile in allFiles)
            {
                //Length of current file name
                int L = curFile.Name.Length;
                //Compare to selected file and check if contains numbers/has same extension
                if (curFile.Name.StartsWith(fstart) && Char.IsNumber(curFile.Name[L - 5]) && curFile.Name.EndsWith(extension))
                {
                    names.Add(Path.Combine(path, curFile.Name));
                }
            }

            //No numbered files found, use selected image
            if (names.Count == 0)
            {
                names.Add(fileName);
            }

            return names;
        }

        /// <summary>
        /// Function for correctly mapping the pixel values, copied from CNTK examples.
        /// </summary>
        /// <param name="pixelFormat">Format of the color data.</param>
        /// <param name="stride">Stride length.</param>
        /// <returns></returns>
        public static Func<int, int, int, int> GetPixelMapper(PixelFormat pixelFormat, int stride)
        {
            switch (pixelFormat)
            {
                /*
                case PixelFormat.Format32bppArgb:
                    return (h, w, c) => h * stride + w * 4 + c;  // bytes are B-G-R-A
                case PixelFormat.Format24bppRgb:
                    return (h, w, c) => h * stride + w * 3 + c;  // bytes are B-G-R
                */
                case PixelFormat.Format8bppIndexed:
                default:
                    return (h, w, c) => h * stride + w;
            }
        }
        

        /// <summary>
        /// Prompts a folderbrowserdialog with given description.
        /// </summary>
        /// <param name="description">Description to be displayed on top of the window.</param>
        /// <returns>Selected path or empty string.</returns>
        public static string GetDirectory(string description)
        {
            var dlg = new FolderBrowserDialog { Description = description };
            DialogResult result = dlg.ShowDialog();

            return DirectoryResult(dlg.SelectedPath, result); // Check that path was selected.
        }

        /// <summary>
        /// Prompts a folderbrowserdialog with given description.
        /// </summary>
        /// <param name="description">Description to be displayed on top of the window.</param>
        /// <returns>Selected path or empty string.</returns>
        public static string GetFile(string description)
        {
            var dlg = new OpenFileDialog { Title = description };
            DialogResult result = dlg.ShowDialog();

            return DirectoryResult(dlg.FileName, result); // Check that path was selected.
        }

        /// <summary>
        /// Test to see if a path was selected during folderbrowserdialog.
        /// </summary>
        /// <param name="selectedPath">Path selected by user.</param>
        /// <param name="result">Result of the dialog. E.g. OK or cancel</param>
        /// <returns>Path or empty string.</returns>
        public static string DirectoryResult(string selectedPath, DialogResult result)
        {
            return result == DialogResult.OK ? selectedPath : string.Empty;
        }

        /// <summary>
        /// Crop volume to selected length.
        /// </summary>
        /// <typeparam name="T">Data type can be selected by user.</typeparam>
        /// <param name="volume">Volume to be cropped.</param>
        /// <param name="dims">Cropping dimensions. Format: x1, x2, y1, y2, z1, z2.</param>
        /// <returns>Cropped volume.</returns>
        public static T[,,] Crop3D<T>(T[,,] volume, int[] dims)
        {
            if (dims.Length != 6)
                throw new Exception("Invalid number of dimensions on dims variable. Include 6 dimensions.");
            T[,,] croppedVolume = new T[dims[1] - dims[0] + 1, dims[3] - dims[2] + 1, dims[5] - dims[4] + 1];

            Parallel.For(dims[0], dims[1] + 1, x =>
            {
                Parallel.For(dims[2], dims[3] + 1, y =>
                {
                    Parallel.For(dims[4], dims[5] + 1, z =>
                    {
                        croppedVolume[x - dims[0], y - dims[2], z - dims[4]] = volume[x, y, z];
                    });
                });
            });
            return croppedVolume;
        }

        public static void get_bbox(out int min_x, out int max_x, out int min_y, out int max_y, Mat input, double threshold = 80.0, double max_val = 255.0)
        {
            Mat BW = input.Threshold(threshold, max_val, ThresholdTypes.Binary);
            var strct = InputArray.Create(new Mat(25, 25, MatType.CV_8UC1));
            BW = BW.Dilate(strct);
            BW = BW.Erode(strct);

            var edges = BW.FindContoursAsMat(RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);
            if (edges.Count() > 0)
            {
                Rect bbox = new Rect();
                int curArea = 0;

                foreach (MatOfPoint contour in edges)
                {
                    var brect = contour.BoundingRect();
                    var area = brect.Height * brect.Width;
                    if (area > curArea)
                    {
                        bbox = brect;
                        curArea = area;
                    }
                }

                if(bbox.Width*bbox.Height > 1600)
                {
                    min_x = bbox.Left;
                    max_x = bbox.Right;
                    min_y = bbox.Top;
                    max_y = bbox.Bottom;
                }
                else
                {
                    min_x = 0;
                    max_x = 0;
                    min_y = 0;
                    max_y = 0;
                }

            }
            else
            {
                min_x = 0;
                max_x = 0;
                min_y = 0;
                max_y = 0;
            }
        }

        public static double get_angle(int[] data, bool radians = false)
        {
            //Compute the mean of the points
            double mu = 0.0;
            for (int k = 0; k < data.Length; k++)
            {
                if(data[k] > 0) { mu += data[k] / data.Length; }
            }

            //Generate points object for fitting
            List<Point2f> points = new List<Point2f>();
            for (int k = 0; k < data.Length; k++)
            {
                if(data[k] > 0)
                {
                    points.Add(new Point2f((float)(k - data.Length / 2), (float)(data[k]-mu)));
                }
            }

            //Fit line
            Line2D line = Cv2.FitLine(points, DistanceTypes.L2, 0, 0.01, 0.01);

            double slope = line.Vy / (line.Vx + 1e-9);

            double theta = Math.Atan(slope);

            if(radians == true)
            {
                return theta;
            }
            else
            {
                theta *= 180.0 / Math.PI;
                return theta;
            }
            
        }

        public static int[,] get_surface_index_from_tiles(double[,,] array, double threshold)
        {
            int h = array.GetLength(0);
            int w = array.GetLength(1);
            int d = array.GetLength(2);

            int[,] idx = new int[h, w];

            Parallel.For(0,h, (int y) =>
            {
                Parallel.For(0, w, (int x) =>
                {
                    for(int k = d-1; k > -1; k-=1)
                    {
                        double val = array[y, x, k];
                        if(val > threshold)
                        {
                            idx[y, x] = k;
                            break;
                        }
                    }
                });
            });

            return idx;
        }

        public static double[] get_tile_angles(int[,] idx, int[] steps)
        {
            //Surface coordinates to points                       

            List<Point2f> pointsx = new List<Point2f>();
            List<Point2f> pointsy = new List<Point2f>();

            for (int y = 0; y < idx.GetLength(0); y++)
            {
                for (int x = 0; x < idx.GetLength(1); x++)
                {
                    pointsx.Add(new Point2f((float)(x * steps[1]), (float)(idx[y, x])));
                    pointsy.Add(new Point2f((float)(y * steps[0]), (float)(idx[y, x])));
                }
            }

            //Surface fit
            Line2D linex = Cv2.FitLine(pointsx, DistanceTypes.L2, 0, 0.01, 0.01);
            Line2D liney = Cv2.FitLine(pointsy, DistanceTypes.L2, 0, 0.01, 0.01);

            //Get angles as degrees
            double thetax = Math.Atan(linex.Vy / (linex.Vx + 1e-9)) * 180.0/Math.PI;
            double thetay = Math.Atan(liney.Vy / (liney.Vx + 1e-9)) * 180.0 / Math.PI;

            return new double[] { thetax, thetay};
        }

        public static vtkImageData get_surface_voi(vtkImageData input, int n_tiles = 64, double threshold = 70.0, double mult = 0.05)
        {
            //Get data dimensions
            int[] dims = input.GetExtent();

            //Get average tiles
            double[,,] tiles; int[] steps;

            threshold *= mult;

            Processing.average_tiles(out tiles, out steps, input, 16);
            //Get surface indices
            int[,] idx = Functions.get_surface_index_from_tiles(tiles, threshold);

            //Find minimum and maximum index
            int min = 65000; int max = 0;
            for (int k1 = 0; k1 < idx.GetLength(0); k1++)
            {
                for (int k2 = 0; k2 < idx.GetLength(1); k2++)
                {
                    if (idx[k1, k2] < min) { min = idx[k1, k2]; }
                    if (idx[k1, k2] > max) { max = idx[k1, k2]; }
                }
            }

            //Crop VOI around minima and maxima
            int[] outextent = new int[] { dims[0], dims[1], dims[2], dims[3], Math.Max(min - 50, dims[4]), Math.Min(max + 50, dims[5]) };

            vtkExtractVOI cropper = vtkExtractVOI.New();
            cropper.SetInput(input);
            cropper.SetVOI(outextent[0], outextent[1], outextent[2], outextent[3], outextent[4], outextent[5]);
            cropper.Update();

            vtkImageData tmpvtk = cropper.GetOutput();

            
            //Get surface orientation
            double[] angles = Functions.get_tile_angles(idx, steps);
            angles = new double[] { angles[0], -angles[1] };
            int[] axes = new int[] { 0, 1 };
            
            
            //Reorient the surface
            for (int k=0; k < angles.Length; k++)
            {
                tmpvtk = Processing.rotate_sample(tmpvtk, angles[k], axes[k], 0);
            }
            
            
            //Detect surface indices and compute mean and standard deviation images
            double[,] mu; double[,] std; vtkImageData output;
            Processing.get_voi_mu_std(out output, out mu, out std, tmpvtk, 25,80.0);
            
            
            //Invert the reorienting            
            for (int k = 0; k < angles.Length; k++)
            {
                output = Processing.rotate_sample(output, -angles[k], axes[k], 0);
            }
            

            return output;
            

        }
    }

    /// <summary>
    /// Image loader, reads images in parallel 
    /// </summary>
    public class ParaLoader
    {
        //Declarations

        //Empty byte array
        byte[,,] data;
        int[] min_x;
        int[] max_x;
        int[] min_y;
        int[] max_y;
        //Empty image data
        vtkImageData vtkdata = vtkImageData.New();
        //Data dimensions
        int[] input_dims = new int[3];
        int[] output_dims = new int[6];
        //Empty list for files
        List<string> files;

        /// <summary>
        /// Set input files
        /// </summary>
        /// <param name="file">File path.</param>
        public void setInput(string file, int[] dims = null)
        {
            //Get files
            files = Functions.getFiles(file);
            //Read image and get dimensions
            Mat _tmp = new Mat(file, ImreadModes.GrayScale);
            input_dims[0] = _tmp.Height;
            input_dims[1] = _tmp.Width;
            input_dims[2] = files.Count;
            //Set output dimensions
            if (dims == null)
            {
                output_dims[0] = 0; output_dims[1] = input_dims[0];
                output_dims[2] = 0; output_dims[3] = input_dims[1];
                output_dims[4] = 0; output_dims[5] = input_dims[2];
            }
            else
            {
                output_dims = dims;
            }
            //Clear temp file
            _tmp.Dispose();

            //Set data extent. Data extent is set, so z-axis is along the
            //first dimension, and y-axis is along the last dimension.
            //This will be reversed when the data gets converted to vtkImagedata.
            data = new byte[output_dims[5] - output_dims[4], output_dims[1] - output_dims[0], output_dims[3] - output_dims[2]];
            min_x = new int[output_dims[5] - output_dims[4]];
            max_x = new int[output_dims[5] - output_dims[4]];
            min_y = new int[output_dims[5] - output_dims[4]];
            max_y = new int[output_dims[5] - output_dims[4]];
        }

        /// <summary>
        /// Read image from file idx. The image is read using OpenCV, and converted to Bitmap.
        /// Bitmap is then read to the bytearray.
        /// </summary>
        /// <param name="idx">File index.</param>
        private void readImage(int idx)
        {
            //Read image from file idx. The image is read using OpenCV, and converted to Bitmap.
            //Bitmap is then read to the bytearray.
            Mat _tmp = new Mat(files[idx], ImreadModes.GrayScale);

            //Get bounding box
            int tmpxmin; int tmpxmax; int tmpymin; int tmpymax;
            Functions.get_bbox(out tmpxmin, out tmpxmax, out tmpymin, out tmpymax, _tmp);
            min_x[idx] = tmpxmin; max_x[idx] = tmpxmax; min_y[idx] = tmpymin; max_y[idx] = tmpymax;            
            
            Bitmap _image = BitmapConverter.ToBitmap(_tmp);
            //Lock bits
            Rectangle _rect = new Rectangle(0, 0, input_dims[1], input_dims[0]);
            BitmapData _bmpData =
                _image.LockBits(_rect, ImageLockMode.ReadOnly, _image.PixelFormat);

            //Get the address of first line
            IntPtr _ptr = _bmpData.Scan0;

            //Declare new array for gray scale values
            int _bytes = Math.Abs(_bmpData.Stride) * _bmpData.Height;
            byte[] _grayValues = new byte[_bytes];

            //Copy the rgb values to the new array
            Marshal.Copy(_ptr, _grayValues, 0, _bytes);

            //Method for correct pixel mapping
            Func<int, int, int, int> mapPixel = Functions.GetPixelMapper(_image.PixelFormat, _bmpData.Stride);

            //Read bits to byte array in parallel
            //Remember the data orientation
            Parallel.For(output_dims[0], output_dims[1], (int h) =>
            {
                Parallel.For(output_dims[2], output_dims[3], (int w) =>
                {
                    data[idx - output_dims[4], h - output_dims[0], w - output_dims[2]] = _grayValues[mapPixel(h, w, 0)];
                });
            });
        }

        /// <summary>
        /// Load all images in parallel
        /// </summary>
        public void Load()
        {
            //Loop over files
            Parallel.For(output_dims[4], output_dims[5], (int d) =>
            {
                readImage(d);
            });
        }

        /// <summary>
        /// Extract data as vtkImageData
        /// </summary>
        /// <returns>Converted data as vtkImageData variable.</returns>
        public vtkImageData GetData(int rotate = 0)
        {
            //Conver byte data to vtkImageData
            vtkdata = DataTypes.byteToVTK(data);
            if (rotate == 0)
            {
                return vtkdata;
            }
            else
            {
                double thetax1 = Functions.get_angle(min_x, false);
                double thetax2 = Functions.get_angle(max_x, false);
                double thetay1 = Functions.get_angle(min_y, false);
                double thetay2 = Functions.get_angle(max_y, false);

                int[] dims = vtkdata.GetExtent();
                int[] centers = new int[] { (dims[1] - dims[0]) / 2, (dims[3] - dims[2]) / 2, (dims[5] - dims[4]) / 2 };

                double[] angles = new double[] { 0.5 * (thetax1 + thetax2), -0.5 * (thetay1 + thetay2) };
                int[] axes = new int[] { 0, 1 };

                for(int k = 0; k<angles.Length; k++)
                {
                    vtkdata = Processing.rotate_sample(vtkdata, angles[k], axes[k],1);
                }                
                
                return vtkdata;
                                
            }

        }
    }

}
