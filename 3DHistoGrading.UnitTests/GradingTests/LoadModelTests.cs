﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using LBPLibrary;
using HistoGrading.Components;

namespace _3DHistoGrading.UnitTests
{
    public class LoadModelTests
    {
        [Fact]
        public void Load_NoModel_ReturnsError()
        {
            // Grading variables
            Model model = new Model();
            string filename = @"\3D-Histo-Grading\Default\calc_parameters.csv";

            Exception ex = Assert.Throws<Exception>(
                delegate { string path = Grading.LoadModel(out model, out Parameters param, filename, filename); });
            Assert.Equal("Could not find weights.dat! Check that default model is on correct folder.", ex.Message);
        }

        [Fact]
        public void Model_DefaultInput_NullParameters()
        {
            Model model = new Model();

            Assert.Null(model.eigenVectors);
            Assert.Null(model.singularValues);
            Assert.Equal(0, model.nComp);
            Assert.Null(model.weights);
        }
    }
}
