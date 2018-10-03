﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

using Kitware.VTK;
using OpenCvSharp;

using HistoGrading.Components;
using HistoGrading.Models;

namespace HistoGrading
{
    public partial class MainForm : Form
    {
        //Settings and Initialization

        //Rendering flags
        int is_rendering = 0;
        int is_mask = 0;

        //Volume dimensions
        int[] dims = new int[] { 0, 0, 0, 0, 0, 0 };
        //Current slice
        int[] sliceN = new int[] { 0, 0, 0 };
        //Current orientation, -1 = 3D rendering
        int ori = -1;

        //Gray values
        int[] gray = new int[2] { 0, 255 };

        //Rendering object
        Rendering.renderPipeLine volume = new Rendering.renderPipeLine();
        
        //Render window
        private vtkRenderWindow renWin;

        //Interactor
        vtkRenderWindowInteractor iactor;

        //Mouse interactor
        bool mouseDown1 = false;
        bool mouseDown2 = false;

        // Grading variables
        Model model = new Model();
        int[,] features = new int[0,0];

        /// <summary>
        /// Form that includes all major components in the software.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Updates GUI text about current rendering method in use.
        /// </summary>
        public void TellSlice()
        {
            //Tell volume rendering
            if(ori == -1)
            {
                sliceLabel.Text = "Rendering volume";
            }
            //Tell coronal rendering
            if (ori == 2)
            {
                sliceLabel.Text = String.Format("Transverse, XY | {0} / {1}", sliceN[ori], dims[5]);
            }
            //Tell transverse rendering
            if (ori == 0)
            {
                sliceLabel.Text = String.Format("Coronal, XZ | {0} / {1}", sliceN[ori], dims[3]);
            }
            //Tell transverse rendering
            if (ori == 1)
            {
                sliceLabel.Text = String.Format("Sagittal, YZ | {0} / {1}", sliceN[ori], dims[1]);
            }
        }

        //Scroll bar updates
        private void updateEvents()
        {
            gmaxBar.ValueChanged += new EventHandler(gmaxBar_ValueChanged);
            gminBar.ValueChanged += new EventHandler(gminBar_ValueChanged);
        }

        private void gmaxBar_ValueChanged(object sender, EventArgs e)
        {
            //Check if rendering
            if (is_rendering == 1)
            {
                //Update gary value range and render volume
                gray[1] = gmaxBar.Value;
                volume.updateCurrent(sliceN,ori,gray);
                volume.setVolumeColor();
                //Update slice if rendering
                if (ori > -1)
                {
                    volume.renderImage();
                    //Render mask
                    if (is_mask == 1)
                    {
                        volume.renderImageMask();
                    }
                }
            }
        }

        private void gminBar_ValueChanged(object sender, EventArgs e)
        {
            //Check if rendering
            if (is_rendering == 1)
            {
                //Update gary value range and render volume
                gray[0] = gminBar.Value;
                volume.updateCurrent(sliceN, ori, gray);
                volume.setVolumeColor();
                //Update slice if rendering
                if(ori>-1)
                {
                    volume.renderImage();
                    //Render mask
                    if(is_mask==1)
                    {
                        volume.renderImageMask();
                    }
                }
            }
        }

        private void sliceBar_ValueChanged(object sender, EventArgs e)
        {
            //Check if rendering slice
            if (ori > -1)
            {
                //Set slice
                sliceN[ori] = sliceBar.Value;
                volume.updateCurrent(sliceN, ori, gray);
                volume.renderImage();
                if (is_mask==1)
                {
                    volume.renderImageMask();
                }
                TellSlice();
            }
        }

        //Render window updates
        private void renderWindowControl_Load(object sender, EventArgs e)
        {
            //Set renderwindow
            renWin = renderWindowControl.RenderWindow;
            //Initialize interactor
            iactor = renWin.GetInteractor();
            iactor.Initialize();
        }

        //Buttons

        //Load CT data
        private void fileButton_Click(object sender, EventArgs e)
        {
            //Select a file and render volume
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                //Update renderwindow
                renderWindowControl_Load(this, null);
                
                //Get path and files
                string impath = fileDialog.FileName;
                string folpath = Path.GetDirectoryName(@impath);

                //Update GUI text to tell path to data folder
                fileLabel.Text = folpath;

                //Load data
                volume.connectData(impath);
                
                //Get dimensions and set slices. Middle slice is set to current slice
                dims = volume.getDims();
                sliceN[0] = (dims[1] - dims[0]) / 2;
                sliceN[1] = (dims[3] - dims[2]) / 2;
                sliceN[2] = (dims[5] - dims[4]) / 2;

                //Connect slice to renderer
                volume.connectWindow(renWin);

                //Render
                volume.renderVolume();

                //Flags for GUI components
                is_rendering = 1;
                is_mask = 0;

                //Orientation
                ori = -1;
                //Update pipeline parameters
                volume.updateCurrent(sliceN, ori, gray);
                volume.setVolumeColor();

                //Update GUI
                maskButton.Text = "Load Mask";
                maskLabel.Text = "No Mask Loaded";
                TellSlice();

                iactor.Enable();

                // Enable buttons
                sagittalButton.Enabled = true;
                coronalButton.Enabled = true;
                transverseButton.Enabled = true;
                volumeButton.Enabled = true;
                resetButton.Enabled = true;
                gminBar.Enabled = true;
                gmaxBar.Enabled = true;
                maskButton.Enabled = true;
                panel2.Enabled = true;
                sliceBar.Enabled = true;
                renderWindowControl.Enabled = true;

                //renderVolumeControl_Load(this, null);
            }
        }

        //Load bone mask
        private void maskButton_Click(object sender, EventArgs e)
        {
            //Dwitch between loading and removing the bone mask
            switch (is_mask)
            {
                case 0:
                    //Check if volume is rendered
                    if (is_rendering == 1)
                    {
                        //Select a file
                        if (fileDialog.ShowDialog() == DialogResult.OK)
                        {
                            //Clear Memory
                            //GC.Collect();

                            //Get path and files
                            string impath = fileDialog.FileName;
                            string extension = Path.GetExtension(@impath);
                            string folpath = Path.GetDirectoryName(@impath);

                            maskLabel.Text = folpath;
                            //Load image data
                            volume.connectMask(impath);
                            //Update pipelin
                            volume.updateCurrent(sliceN, ori, gray);

                        //Render
                        if (ori == -1)
                            {
                                volume.renderVolumeMask();
                                volume.setVolumeColor();
                            }
                            if (ori > -1)
                            {
                                volume.renderImageMask();
                            }

                            //Update flags
                            is_mask = 1;
                            maskButton.Text = "Remove Mask";
                        }
                    }
                    break;

                case 1:
                    volume.removeMask();
                    volume.updateCurrent(sliceN, ori, gray);
                    if (ori == -1)
                    {
                        volume.renderVolume();
                        TellSlice();
                    }
                    if (ori > -1)
                    {
                        volume.renderImage();
                        TellSlice();
                    }
                    is_mask = 0;
                    maskButton.Text = "Load Mask";
                    maskLabel.Text = "No Mask Loaded";
            break;
            }
        }

        //Reset camera
        private void resetButton_Click(object sender, EventArgs e)
        {
            //Memory management
            //GC.Collect();
            if (is_rendering == 1)
            {
                volume.resetCamera();

                //Reset slices
                sliceN[0] = (dims[1] - dims[0]) / 2;
                sliceN[1] = (dims[3] - dims[2]) / 2;
                sliceN[2] = (dims[5] - dims[4]) / 2;

                if (ori > -1)
                {
                    volume.updateCurrent(sliceN, ori, gray);
                    volume.renderImage();
                    if(is_mask == 1)
                    {
                        volume.renderImageMask();
                    }
                }
                TellSlice();
            }
        }

        //Render volume
        private void volumeButton_Click(object sender, EventArgs e)
        {
            //Update parameters
            ori = -1;
            volume.updateCurrent(sliceN, ori, gray);
            //Render volume
            volume.renderVolume();
            volume.setVolumeColor();

            if (is_mask==1)
            {
                volume.renderVolumeMask();
            }
            TellSlice();
            iactor.Enable();
        }

        //Render coronal slice
        private void transverseButton_Click(object sender, EventArgs e)
        {
            if (is_rendering == 1)
            {
                //Set orientation
                ori = 2;
                //Update scroll bar
                sliceBar.Maximum = dims[5];
                sliceBar.Value = sliceN[2];
                //Update rendering pipeline and render
                volume.updateCurrent(sliceN, ori, gray);
                volume.renderImage();
                //Check mask
                if (is_mask == 1)
                {
                    volume.renderImageMask();
                }
                TellSlice();
                iactor.Disable();
            }
        }

        //Render transverse slice, XZ plane
        private void coronalButton_Click(object sender, EventArgs e)
        {
            if (is_rendering == 1)
            {
                //Set orientation
                ori = 0;
                //Update scroll bar
                sliceBar.Maximum = dims[1];
                sliceBar.Value = sliceN[0];
                //Update rendering pipeline and render
                volume.updateCurrent(sliceN, ori, gray);
                volume.renderImage();
                //Check mask
                if (is_mask == 1)
                {
                    volume.renderImageMask();
                }
                TellSlice();
                iactor.Disable();
            }
        }

        //Render transverse slice, YZ plane
        private void sagittalButton_Click(object sender, EventArgs e)
        {
            //Check if rendering
            if(is_rendering==1)
            {
                //Set orientation
                ori = 1;
                //Update scroll bar
                sliceBar.Maximum = dims[3];
                sliceBar.Value = sliceN[1];
                //Update rendering pipeline and render
                volume.updateCurrent(sliceN, ori, gray);
                volume.renderImage();
                //Check mask
                if(is_mask==1)
                {
                    volume.renderImageMask();
                }
                TellSlice();
                iactor.Disable();
            }
        }

        // Predict OA grade
        private void predict_Click(object sender, EventArgs e)
        {
            string grade = Grading.Predict(model, ref features, ref volume);
            sliceLabel.Text = grade;
        }

        //Scroll bars

        //Scroll slices
        private void sliceBar_Scroll(object sender, ScrollEventArgs e)
        {
            //Call at the end of scroll event
            if (e.Type == ScrollEventType.EndScroll)
            {
                sliceBar_ValueChanged(this, null);
                TellSlice();
            }
        }

        //Scroll gray max
        private void gmaxBar_Scroll(object sender, ScrollEventArgs e)
        {
            //Call at the end of scroll event
            if (e.Type == ScrollEventType.EndScroll)
            {
                gmaxBar_ValueChanged(this, null);
            }
        }

        //Scroll gray min
        private void gminBar_Scroll(object sender, ScrollEventArgs e)
        {
            //Call at the end of scroll event
            if (e.Type == ScrollEventType.EndScroll)
            {
                gminBar_ValueChanged(this, null);
            }
        }

        private void segmentButton_Click(object sender, EventArgs e)
        {
            //VOI for segmentation

            //Get sample dimensions
            int[] extent = volume.getDims();            

            //480*416 VOI from the center
            int[] voi_extent = new int[] { extent[0], extent[1], extent[2], extent[3], 20, 20+447 };
            int[] batch_dims = new int[] { 448, 448, 1 };

            //Segmentation
            List<vtkImageData> outputs;
            IO.segmentation_pipeline(out outputs, volume, batch_dims, voi_extent, new int[] { 0 }, 32, 0.7 * 255.0);

            /*
            vtkImageMathematics math = vtkImageMathematics.New();
            math.SetInput1(outputs.ElementAt(0));
            math.SetInput2(outputs.ElementAt(1));

            math.SetOperationToAdd();
            math.Update();
            */

            volume.connectMaskFromData(outputs.ElementAt(0), 0);

            //Update rendering pipeline
            maskLabel.Text = "Automatic";

            //Render
            if (ori == -1)
            {
                volume.renderVolumeMask();
                volume.setVolumeColor();
            }
            if (ori > -1)
            {
                volume.renderImageMask();
            }

        }

        private void cropButton_Click(object sender, EventArgs e)
        {


            /*
            vtkImageData center = vtkImageData.New();
            center.DeepCopy(volume.getVOI());
            center = Processing.center_crop(center);
            */
            //Connect mask to segmentation pipeline
            volume.center_crop(448);
            //Update pipeline
            volume.updateCurrent(sliceN, ori, gray);

            //Render
            if (ori == -1)
            {
                volume.renderVolume();
                volume.setVolumeColor();
            }
            if (ori > -1)
            {
                volume.renderImage();
            }

            //Update flags
            is_mask = 1;
            maskButton.Text = "Remove Mask";
        }
    }
}
