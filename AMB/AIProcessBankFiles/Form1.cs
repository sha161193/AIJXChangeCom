using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;

namespace AIProcessBankFiles
{
    public partial class Form1 : Form
    {
        public Form1(string[] args)
        {
            if (args.Length == 1)
            {
                _IFaceTypeID = Convert.ToInt32(args[0]);            
            }
            _IFaceTypeID = 68;
            InitializeComponent();
        }

        private int _IFaceTypeID = 0;       
        private void Form1_Load(object sender, EventArgs e)
        {
            if(_IFaceTypeID == 0)
            {
                Close();
                return;
            }
            ProcessBankFiles AIBankFileProcessor = null;
            switch (_IFaceTypeID)
            {
                case 8:
                    AIBankFileProcessor = new FCBProcessBankFiles(_IFaceTypeID);
                    break;
                case 22:
                    AIBankFileProcessor = new NSBProcessBankFiles(_IFaceTypeID);
                    break;
                case 33:
                    AIBankFileProcessor = new BNCProcessBankFiles(_IFaceTypeID);
                    break;
                case 37:
                    AIBankFileProcessor = new FCNBProcessBankFiles(_IFaceTypeID);
                    break;
                case 40:
                    AIBankFileProcessor = new MPBProcessBankFiles(_IFaceTypeID);
                    break;
                case 44:
                    AIBankFileProcessor = new CFBDCProcessBankFiles(_IFaceTypeID);
                    break;
                case 45:
                    AIBankFileProcessor = new QBProcessBankFiles(_IFaceTypeID);
                    break;
                case 46:
                    AIBankFileProcessor = new BPOPProcessBankFiles(_IFaceTypeID);
                    break;
                case 47:
                    AIBankFileProcessor = new MBProcessBankFiles(_IFaceTypeID);
                    break;
                case 52:
                    AIBankFileProcessor = new GBProcessBankFiles(_IFaceTypeID);
                    break;
                case 66:
                    AIBankFileProcessor = new CWBProcessBankFiles(_IFaceTypeID);
                    break;
                case 67:
                    AIBankFileProcessor = new VNBProcessBankFiles(_IFaceTypeID);
                    break;
                case 68:
                    AIBankFileProcessor = new AMBProcessBankFiles(_IFaceTypeID);
                    break;
                case 70:
                    AIBankFileProcessor = new PCBProcessBankFiles(_IFaceTypeID);
                    break;
                case 71:
                    AIBankFileProcessor = new SCBProcessBankFiles(_IFaceTypeID);
                    break;
                case 76:
                    AIBankFileProcessor = new IBProcessBankFiles(_IFaceTypeID);
                    break;
                default:
                    Close();
                    return;
            }           
            if (AIBankFileProcessor != null)
                AIBankFileProcessor.ProcessFiles();
            AIBankFileProcessor.Dispose();
            Close(); 
        }
       
    }
}