﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using HistoGrading.Components;
using System.Windows.Forms;
using Accord.Math;
using System.Drawing;
using System.IO;

namespace _3DHistoGrading.UnitTests.GradingTests
{
    public class ProcessingTests
    {
        TestImage testImg = new TestImage(); // Initialize testimage function

        [Fact]
        public void SubtractMean_TestImage_EqualsReference()
        {
            testImg.New("Quarters", new int[] { 4, 4 });

            double[,] subtracted = Processing.SubtractMean(testImg.Image.ToDouble());

            double[,] refArray = new double[4, 4] // Here, actually columns are written out
                { { -1, -1, -1, -1},
                { -1, -1, -1, -1},
                { 1, 1, 1, 1},
                { 1, 1, 1, 1} };
            Assert.Equal(subtracted, refArray);
        }

        [Fact]
        public void GetCenter_TestImage_EqualsReference()
        {
            testImg.New("Quarters", new int[] { 20, 20 });
            byte[,] slice = testImg.Image.ToByte();
            byte[,,] volume = new byte[20, 20, 1];
            for (int i = 0; i < slice.GetLength(0); i++)
            {
                for (int j = 0; j < slice.GetLength(1); j++)
                {
                    volume[i, j, 0] = slice[i, j];
                }
            }

            int[] center = Processing.GetCenter(volume, 3);

            Assert.Equal(new int[] { 14, 14}, center);
        }

        [Fact]
        public void ReadCSV_AbleToRead_ParameterArray()
        {
            string path =
                    new DirectoryInfo(Directory.GetCurrentDirectory()) // Get current directory
                    .Parent.Parent.Parent.FullName; // Move to correct location and add file name

            Console.WriteLine(path);
            path = path + ".\\Default\\deep_parameters.csv";
            var param = DataTypes.ReadCSV(path);

            //Assert.Equal(new int[] { 14, 14 }, center);
        }

        [Fact]
        public void GetSurface_TestImage_EqualsReference()
        {
            testImg.New("Quarters", new int[] { 20, 20 });
            byte[,] slice = testImg.Image.ToByte();
            byte[,,] volume = new byte[20, 20, 3];
            for (int i = 0; i < slice.GetLength(0); i++)
            {
                for (int j = 0; j < slice.GetLength(1); j++)
                {
                    volume[i, j, 1] = slice[i, j];
                }
            }

            Processing.GetSurface(volume, new int[] { 14, 14 }, new int[] { 2, 2 }, 3,
            out int[,] surfaceCoordinates, out byte[,,] surfaceVOI);

            byte[,,] refSurface = new byte[2, 2, 2]
                { {{ 4, 0},
                { 4, 0} },
                { { 4, 0},
                { 4, 0} }};
            Assert.Equal(new int[,] { { 1, 1 }, { 1, 1 } }, surfaceCoordinates);
            Assert.Equal(refSurface, surfaceVOI);
        }

        [Fact]
        public void MeanAndStd_TestImage_EqualsReference()
        {
            testImg.New("Quarters", new int[] { 4, 4 });
            byte[,] slice = testImg.Image.ToByte();
            byte[,,] volume = new byte[4, 4, 3];
            for (int i = 0; i < slice.GetLength(0); i++)
            {
                for (int j = 0; j < slice.GetLength(1); j++)
                {
                    volume[i, j, 1] = slice[i, j];
                }
            }
            /*
            Processing.MeanAndStd(volume, out double[,] meanImage, out double[,] stdImage);

            double[,] refMean = new double[4, 4]
                { { 0.33333333333333331, 0.33333333333333331, 1, 1},
                { 0.33333333333333331, 0.33333333333333331, 1, 1},
                { 0.66666666666666663, 0.66666666666666663, 1.3333333333333333, 1.3333333333333333},
                { 0.66666666666666663, 0.66666666666666663, 1.3333333333333333, 1.3333333333333333} };
            double[,] refStd = new double[4, 4]
                { { 0.57735026918962584, 0.57735026918962584, 1.7320508075688772, 1.7320508075688772},
                { 0.57735026918962584, 0.57735026918962584, 1.7320508075688772, 1.7320508075688772},
                { 1.1547005383792517, 1.1547005383792517, 2.3094010767585034, 2.3094010767585034},
                { 1.1547005383792517, 1.1547005383792517, 2.3094010767585034, 2.3094010767585034} };
            Assert.Equal(refMean, meanImage);
            Assert.Equal(refStd, stdImage);
            */
        }
    }
}
