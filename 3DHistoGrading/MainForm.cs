﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Kitware.VTK;

using HistoGrading.Components;

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

        //Mouse interactor
        bool mouseDown1 = false;
        bool mouseDown2 = false;

        // Grading variables
        Model model = new Model();
        int[,] features = new int[0,0];

        //Form initialization
        public MainForm()
        {
            InitializeComponent();
        }

        //GUI text update
        public void tellSlice()
        {
            //Tell volume rendering
            if(ori == -1)
            {
                sliceLabel.Text = "Rendering volume";
            }
            //Tell coronal rendering
            if (ori == 2)
            {
                sliceLabel.Text = String.Format("Coronal | {0} / {1}", sliceN[ori], dims[5]);
            }
            //Tell transverse rendering
            if (ori == 0)
            {
                sliceLabel.Text = String.Format("Transverse, XZ | {0} / {1}", sliceN[ori], dims[3]);
            }
            //Tell transverse rendering
            if (ori == 1)
            {
                sliceLabel.Text = String.Format("Transverse, YZ | {0} / {1}", sliceN[ori], dims[1]);
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
                tellSlice();
            }
        }

        //Render window updates
        private void renderWindowControl_Load(object sender, EventArgs e)
        {
            //Set renderwindow
            renWin = renderWindowControl.RenderWindow;
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
                tellSlice();

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
                        tellSlice();
                    }
                    if (ori > -1)
                    {
                        volume.renderImage();
                        tellSlice();
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
                tellSlice();
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
            tellSlice();
        }

        //Render coronal slice
        private void coronalButton_Click(object sender, EventArgs e)
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
                tellSlice();
            }
        }

        //Render transverse slice, XZ plane
        private void transverse1Button_Click(object sender, EventArgs e)
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
                tellSlice();
            }
        }

        //Render transverse slice, YZ plane
        private void transverse2Button_Click(object sender, EventArgs e)
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
                tellSlice();
            }
        }

        // Load Grading model
        private void loadModel_Click(object sender, EventArgs e)
        {
            sliceLabel.Text = "Loaded model";
            Grading.LoadModel(ref model);
        }

        // Predict OA grade
        private void predict_Click(object sender, EventArgs e)
        {
            double grade = Grading.Predict(model, ref features);
            sliceLabel.Text = "OA grade: " + grade.ToString("####.##");
        }

        //Scroll bars

        //Scroll slices
        private void sliceBar_Scroll(object sender, ScrollEventArgs e)
        {
            //Call at the end of scroll event
            if (e.Type == ScrollEventType.EndScroll)
            {
                sliceBar_ValueChanged(this, null);
                tellSlice();
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
    }
}