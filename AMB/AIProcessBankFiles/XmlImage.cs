using System;
using System.Collections.Generic;
using System.Text;

namespace AIProcessBankFiles
{
    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class Root
    {

        private RootImageRecord[] imageRecordField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("ImageRecord")]
        public RootImageRecord[] ImageRecord
        {
            get
            {
                return this.imageRecordField;
            }
            set
            {
                this.imageRecordField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class RootImageRecord
    {

        private RootImageRecordFrontImage frontImageField;

        private RootImageRecordBackImage backImageField;

        /// <remarks/>
        public RootImageRecordFrontImage FrontImage
        {
            get
            {
                return this.frontImageField;
            }
            set
            {
                this.frontImageField = value;
            }
        }

        /// <remarks/>
        public RootImageRecordBackImage BackImage
        {
            get
            {
                return this.backImageField;
            }
            set
            {
                this.backImageField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class RootImageRecordFrontImage
    {

        private byte errorIDField;

        private string errorDescField;

        private RootImageRecordFrontImageItem itemField;

        private object pagesField;

        private uint sizeField;

        private string imageFormatField;

        private string imageDataField;

        /// <remarks/>
        public byte ErrorID
        {
            get
            {
                return this.errorIDField;
            }
            set
            {
                this.errorIDField = value;
            }
        }

        /// <remarks/>
        public string ErrorDesc
        {
            get
            {
                return this.errorDescField;
            }
            set
            {
                this.errorDescField = value;
            }
        }

        /// <remarks/>
        public RootImageRecordFrontImageItem Item
        {
            get
            {
                return this.itemField;
            }
            set
            {
                this.itemField = value;
            }
        }

        /// <remarks/>
        public object Pages
        {
            get
            {
                return this.pagesField;
            }
            set
            {
                this.pagesField = value;
            }
        }

        /// <remarks/>
        public uint Size
        {
            get
            {
                return this.sizeField;
            }
            set
            {
                this.sizeField = value;
            }
        }

        /// <remarks/>
        public string ImageFormat
        {
            get
            {
                return this.imageFormatField;
            }
            set
            {
                this.imageFormatField = value;
            }
        }

        /// <remarks/>
        public string ImageData
        {
            get
            {
                return this.imageDataField;
            }
            set
            {
                this.imageDataField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class RootImageRecordFrontImageItem
    {

        private uint hostImageNumberField;

        private uint processingDateField;

        private object runField;

        private object batchField;

        private object seqField;

        private object endorsementField;

        private uint accountField;

        private object accountTypeField;

        private string amountField;

        private uint serialField;

        private object tranCodeField;

        private uint tranRoutingField;

        private object dRCRField;

        private object itemNumberField;

        private object pocketField;

        private object tsetField;

        private object itemTypeField;

        private object mICRLineField;

        /// <remarks/>
        public uint HostImageNumber
        {
            get
            {
                return this.hostImageNumberField;
            }
            set
            {
                this.hostImageNumberField = value;
            }
        }

        /// <remarks/>
        public uint ProcessingDate
        {
            get
            {
                return this.processingDateField;
            }
            set
            {
                this.processingDateField = value;
            }
        }

        /// <remarks/>
        public object Run
        {
            get
            {
                return this.runField;
            }
            set
            {
                this.runField = value;
            }
        }

        /// <remarks/>
        public object Batch
        {
            get
            {
                return this.batchField;
            }
            set
            {
                this.batchField = value;
            }
        }

        /// <remarks/>
        public object Seq
        {
            get
            {
                return this.seqField;
            }
            set
            {
                this.seqField = value;
            }
        }

        /// <remarks/>
        public object Endorsement
        {
            get
            {
                return this.endorsementField;
            }
            set
            {
                this.endorsementField = value;
            }
        }

        /// <remarks/>
        public uint Account
        {
            get
            {
                return this.accountField;
            }
            set
            {
                this.accountField = value;
            }
        }

        /// <remarks/>
        public object AccountType
        {
            get
            {
                return this.accountTypeField;
            }
            set
            {
                this.accountTypeField = value;
            }
        }

        /// <remarks/>
        public string Amount
        {
            get
            {
                return this.amountField;
            }
            set
            {
                this.amountField = value;
            }
        }

        /// <remarks/>
        public uint Serial
        {
            get
            {
                return this.serialField;
            }
            set
            {
                this.serialField = value;
            }
        }

        /// <remarks/>
        public object TranCode
        {
            get
            {
                return this.tranCodeField;
            }
            set
            {
                this.tranCodeField = value;
            }
        }

        /// <remarks/>
        public uint TranRouting
        {
            get
            {
                return this.tranRoutingField;
            }
            set
            {
                this.tranRoutingField = value;
            }
        }

        /// <remarks/>
        public object DRCR
        {
            get
            {
                return this.dRCRField;
            }
            set
            {
                this.dRCRField = value;
            }
        }

        /// <remarks/>
        public object ItemNumber
        {
            get
            {
                return this.itemNumberField;
            }
            set
            {
                this.itemNumberField = value;
            }
        }

        /// <remarks/>
        public object Pocket
        {
            get
            {
                return this.pocketField;
            }
            set
            {
                this.pocketField = value;
            }
        }

        /// <remarks/>
        public object Tset
        {
            get
            {
                return this.tsetField;
            }
            set
            {
                this.tsetField = value;
            }
        }

        /// <remarks/>
        public object ItemType
        {
            get
            {
                return this.itemTypeField;
            }
            set
            {
                this.itemTypeField = value;
            }
        }

        /// <remarks/>
        public object MICRLine
        {
            get
            {
                return this.mICRLineField;
            }
            set
            {
                this.mICRLineField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class RootImageRecordBackImage
    {

        private byte errorIDField;

        private string errorDescField;

        private RootImageRecordBackImageItem itemField;

        private object pagesField;

        private uint sizeField;

        private string imageFormatField;

        private string imageDataField;

        /// <remarks/>
        public byte ErrorID
        {
            get
            {
                return this.errorIDField;
            }
            set
            {
                this.errorIDField = value;
            }
        }

        /// <remarks/>
        public string ErrorDesc
        {
            get
            {
                return this.errorDescField;
            }
            set
            {
                this.errorDescField = value;
            }
        }

        /// <remarks/>
        public RootImageRecordBackImageItem Item
        {
            get
            {
                return this.itemField;
            }
            set
            {
                this.itemField = value;
            }
        }

        /// <remarks/>
        public object Pages
        {
            get
            {
                return this.pagesField;
            }
            set
            {
                this.pagesField = value;
            }
        }

        /// <remarks/>
        public uint Size
        {
            get
            {
                return this.sizeField;
            }
            set
            {
                this.sizeField = value;
            }
        }

        /// <remarks/>
        public string ImageFormat
        {
            get
            {
                return this.imageFormatField;
            }
            set
            {
                this.imageFormatField = value;
            }
        }

        /// <remarks/>
        public string ImageData
        {
            get
            {
                return this.imageDataField;
            }
            set
            {
                this.imageDataField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class RootImageRecordBackImageItem
    {

        private uint hostImageNumberField;

        private uint processingDateField;

        private object runField;

        private object batchField;

        private object seqField;

        private object endorsementField;

        private uint accountField;

        private object accountTypeField;

        private string amountField;

        private uint serialField;

        private object tranCodeField;

        private uint tranRoutingField;

        private object dRCRField;

        private object itemNumberField;

        private object pocketField;

        private object tsetField;

        private object itemTypeField;

        private object mICRLineField;

        /// <remarks/>
        public uint HostImageNumber
        {
            get
            {
                return this.hostImageNumberField;
            }
            set
            {
                this.hostImageNumberField = value;
            }
        }

        /// <remarks/>
        public uint ProcessingDate
        {
            get
            {
                return this.processingDateField;
            }
            set
            {
                this.processingDateField = value;
            }
        }

        /// <remarks/>
        public object Run
        {
            get
            {
                return this.runField;
            }
            set
            {
                this.runField = value;
            }
        }

        /// <remarks/>
        public object Batch
        {
            get
            {
                return this.batchField;
            }
            set
            {
                this.batchField = value;
            }
        }

        /// <remarks/>
        public object Seq
        {
            get
            {
                return this.seqField;
            }
            set
            {
                this.seqField = value;
            }
        }

        /// <remarks/>
        public object Endorsement
        {
            get
            {
                return this.endorsementField;
            }
            set
            {
                this.endorsementField = value;
            }
        }

        /// <remarks/>
        public uint Account
        {
            get
            {
                return this.accountField;
            }
            set
            {
                this.accountField = value;
            }
        }

        /// <remarks/>
        public object AccountType
        {
            get
            {
                return this.accountTypeField;
            }
            set
            {
                this.accountTypeField = value;
            }
        }

        /// <remarks/>
        public string Amount
        {
            get
            {
                return this.amountField;
            }
            set
            {
                this.amountField = value;
            }
        }

        /// <remarks/>
        public uint Serial
        {
            get
            {
                return this.serialField;
            }
            set
            {
                this.serialField = value;
            }
        }

        /// <remarks/>
        public object TranCode
        {
            get
            {
                return this.tranCodeField;
            }
            set
            {
                this.tranCodeField = value;
            }
        }

        /// <remarks/>
        public uint TranRouting
        {
            get
            {
                return this.tranRoutingField;
            }
            set
            {
                this.tranRoutingField = value;
            }
        }

        /// <remarks/>
        public object DRCR
        {
            get
            {
                return this.dRCRField;
            }
            set
            {
                this.dRCRField = value;
            }
        }

        /// <remarks/>
        public object ItemNumber
        {
            get
            {
                return this.itemNumberField;
            }
            set
            {
                this.itemNumberField = value;
            }
        }

        /// <remarks/>
        public object Pocket
        {
            get
            {
                return this.pocketField;
            }
            set
            {
                this.pocketField = value;
            }
        }

        /// <remarks/>
        public object Tset
        {
            get
            {
                return this.tsetField;
            }
            set
            {
                this.tsetField = value;
            }
        }

        /// <remarks/>
        public object ItemType
        {
            get
            {
                return this.itemTypeField;
            }
            set
            {
                this.itemTypeField = value;
            }
        }

        /// <remarks/>
        public object MICRLine
        {
            get
            {
                return this.mICRLineField;
            }
            set
            {
                this.mICRLineField = value;
            }
        }
    }


}
