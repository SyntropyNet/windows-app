﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SyntropyNet.WindowsApp.Views
{
    /// <summary>
    /// Interaction logic for AddToken.xaml
    /// </summary>
    public partial class AddToken : UserControl
    {
        public AddToken()
        {
            InitializeComponent();
            BrushConverter bc = new BrushConverter();
            addBtn.Background = (Brush)bc.ConvertFrom("#0178d4");
        }
    }
}
