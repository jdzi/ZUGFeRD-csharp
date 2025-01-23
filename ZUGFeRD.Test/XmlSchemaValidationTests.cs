/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

namespace s2industries.ZUGFeRD.Test
{
    [TestClass]
    public class XmlSchemaValidationTests : TestBase
    {
        private InvoiceProvider _InvoiceProvider = new InvoiceProvider();

        private static XmlSchemaSet LoadSchemas(string directory)
        {
            XmlSchemaSet schemas = new XmlSchemaSet()
            {
                XmlResolver = XmlResolver.FileSystemResolver
            };

            foreach (var file in Directory.EnumerateFiles(directory, "*.xsd", SearchOption.AllDirectories))
            {
                if (file.EndsWith("UBL-xmldsig-core-schema-2.1.xsd"))
                {
                    XmlSchema? schema = LoadSchemaWithoutDtdProcessing(file);

                    if (schema != null)
                        schemas.Add(schema);
                }
                else
                {
                    schemas.Add(null, file);
                }                    
            }

            schemas.ValidationEventHandler += (object? sender, ValidationEventArgs args) => { /* Don't throw on errors */ };
            schemas.Compile();

            return schemas;
        }

        private static XmlSchema? LoadSchemaWithoutDtdProcessing(string schemaFile)
        {
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };

            using (XmlReader reader = XmlReader.Create(schemaFile, settings))
            {
                return XmlSchema.Read(reader, new ValidationEventHandler((object? sender, ValidationEventArgs args) => { /* Don't throw on errors */ }));
            }
        }

        private static List<ValidationEventArgs> Validate(MemoryStream xmlStream, string schemaDirectory)
        {
            var schemas = LoadSchemas(schemaDirectory);

            var settings = new XmlReaderSettings
            {
                //DtdProcessing = DtdProcessing.Ignore,
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ProcessInlineSchema
                                | XmlSchemaValidationFlags.ProcessSchemaLocation
                                | XmlSchemaValidationFlags.ProcessIdentityConstraints
                                | XmlSchemaValidationFlags.ReportValidationWarnings,
                //XmlResolver = XmlResolver.FileSystemResolver,
                Schemas = schemas,
            };


            List<ValidationEventArgs> results = new List<ValidationEventArgs>();

            settings.ValidationEventHandler += (object? sender, ValidationEventArgs args) =>
            {
                results.Add(args);
            };

            using (XmlReader reader = XmlReader.Create(xmlStream, settings))
            {
                while (reader.Read()) { /* Das Schema wird beim Lesen durch den Reader validiert */ }
            }

            return results;
        }

        private static List<string> FormatErrors(List<ValidationEventArgs> results, MemoryStream xmlStream)
        {
            List<string> formatedErrors = new List<string>(results.Count);

            if (results.Count == 0)
                return formatedErrors;

            xmlStream.Seek(0, SeekOrigin.Begin);

            List<string> lines = new List<string>();

            using (StreamReader reader = new StreamReader(xmlStream, leaveOpen: true))
            {
                for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    lines.Add(line);
            }

            foreach (var result in results)
            {
                StringBuilder msg = WrapText(result.Message);

                msg.AppendLine();
                msg.AppendLine("LineNumber: " + result.Exception.LineNumber + ", LinePosition: " + result.Exception.LinePosition);

                const int overlapCount = 5;

                for (int i = result.Exception.LineNumber - overlapCount; i < result.Exception.LineNumber + overlapCount; i++)
                {
                    if (i >= 0 && i < lines.Count)
                        msg.AppendLine($"{i + 1}:{lines[i]}");
                }


                formatedErrors.Add(msg.ToString());
            }

            return formatedErrors;
        }

        private static StringBuilder WrapText(string message)
        {
            string[] words = message.Split(' ', StringSplitOptions.None);

            StringBuilder result = new StringBuilder(message.Length);
            int lineLength = 0;

            foreach (var word in words)
            {
                if (lineLength > 0)
                {
                    lineLength++;
                    result.Append(" ");
                }

                if (lineLength + word.Length > 120)
                {
                    result.AppendLine();
                    lineLength = 0;
                }

                result.Append(word);
                lineLength += word.Length;
            }

            return result;
        }

        private InvoiceDescriptor CreateInvoiceDescriptor()
        {
            InvoiceDescriptor desc = this._InvoiceProvider.CreateInvoice();
            string filename2 = "myrandomdata.bin";
            DateTime timestamp = DateTime.Now.Date;
            byte[] data = new byte[32768];
            new Random().NextBytes(data);

            desc.AddAdditionalReferencedDocument(
                id: "My-File-BIN",
                typeCode: AdditionalReferencedDocumentTypeCode.ReferenceDocument,
                issueDateTime: timestamp.AddDays(-2),
                name: "EmbeddedPdf",
                attachmentBinaryObject: data,
                filename: filename2);

            desc.OrderNo = "12345";
            desc.OrderDate = timestamp;

            desc.SetContractReferencedDocument("12345", timestamp);

            desc.SpecifiedProcuringProject = new SpecifiedProcuringProject { ID = "123", Name = "Project 123" };

            desc.ShipTo = new Party
            {
                ID = new GlobalID(GlobalIDSchemeIdentifiers.Unknown, "123"),
                GlobalID = new GlobalID(GlobalIDSchemeIdentifiers.DUNS, "789"),
                Name = "Ship To",
                ContactName = "Max Mustermann",
                Street = "Münchnerstr. 55",
                Postcode = "83022",
                City = "Rosenheim",
                Country = CountryCodes.DE
            };

            desc.UltimateShipTo = new Party
            {
                ID = new GlobalID(GlobalIDSchemeIdentifiers.Unknown, "123"),
                GlobalID = new GlobalID(GlobalIDSchemeIdentifiers.DUNS, "789"),
                Name = "Ultimate Ship To",
                ContactName = "Max Mustermann",
                Street = "Münchnerstr. 55",
                Postcode = "83022",
                City = "Rosenheim",
                Country = CountryCodes.DE
            };

            desc.ShipFrom = new Party
            {
                ID = new GlobalID(GlobalIDSchemeIdentifiers.Unknown, "123"),
                GlobalID = new GlobalID(GlobalIDSchemeIdentifiers.DUNS, "789"),
                Name = "Ship From",
                ContactName = "Eva Musterfrau",
                Street = "Alpenweg 5",
                Postcode = "83022",
                City = "Rosenheim",
                Country = CountryCodes.DE
            };

            desc.PaymentMeans.SEPACreditorIdentifier = "SepaID";
            desc.PaymentMeans.SEPAMandateReference = "SepaMandat";
            desc.PaymentMeans.FinancialCard = new FinancialCard { Id = "123", CardholderName = "Mustermann" };

            desc.PaymentReference = "PaymentReference";

            desc.Invoicee = new Party()
            {
                Name = "Test",
                ContactName = "Max Mustermann",
                Postcode = "83022",
                City = "Rosenheim",
                Street = "Münchnerstraße 123",
                AddressLine3 = "EG links",
                CountrySubdivisionName = "Bayern",
                Country = CountryCodes.DE
            };

            desc.Payee = new Party() 
            {
                Name = "Test",
                ContactName = "Max Mustermann",
                Postcode = "83022",
                City = "Rosenheim",
                Street = "Münchnerstraße 123",
                AddressLine3 = "EG links",
                CountrySubdivisionName = "Bayern",
                Country = CountryCodes.DE
            };

            desc.AddDebitorFinancialAccount(iban: "DE02120300000000202052", bic: "BYLADEM1001", bankName: "Musterbank");
            desc.BillingPeriodStart = timestamp;
            desc.BillingPeriodEnd = timestamp.AddDays(14);

            desc.AddTradeAllowanceCharge(false, 5m, CurrencyCodes.EUR, 15m, "Reason for charge", TaxTypes.AAB, TaxCategoryCodes.AB, 19m, AllowanceReasonCodes.Packaging);
            desc.AddLogisticsServiceCharge(10m, "Logistics service charge", TaxTypes.AAC, TaxCategoryCodes.AC, 7m);

            desc.GetTradePaymentTerms().FirstOrDefault().DueDate = timestamp.AddDays(14);
            desc.AddInvoiceReferencedDocument("RE-12345", timestamp);

            //set additional LineItem data
            var lineItem = desc.TradeLineItems.FirstOrDefault(i => i.SellerAssignedID == "TB100A4");
            Assert.IsNotNull(lineItem);

            lineItem.Description = "This is line item TB100A4";
            lineItem.BuyerAssignedID = "0815";
            lineItem.SetOrderReferencedDocument("12345", timestamp, "1");
            lineItem.SetDeliveryNoteReferencedDocument("12345", timestamp);
            lineItem.SetContractReferencedDocument("12345", timestamp);

            lineItem.AddAdditionalReferencedDocument("xyz", AdditionalReferencedDocumentTypeCode.ReferenceDocument, ReferenceTypeCodes.AAB, timestamp);
            lineItem.AddAdditionalReferencedDocument("abc", AdditionalReferencedDocumentTypeCode.InvoiceDataSheet, ReferenceTypeCodes.PP, timestamp);

            lineItem.UnitQuantity = 3m;
            lineItem.ActualDeliveryDate = timestamp;

            lineItem.ApplicableProductCharacteristics.Add(new ApplicableProductCharacteristic
            {
                Description = "Product characteristics",
                Value = "Product value"
            });

            lineItem.BillingPeriodStart = timestamp;
            lineItem.BillingPeriodEnd = timestamp.AddDays(10);

            lineItem.AddReceivableSpecifiedTradeAccountingAccount("987654");
            lineItem.AddTradeAllowanceCharge(false, CurrencyCodes.EUR, 10m, 50m, "Reason: UnitTest", AllowanceReasonCodes.Packaging);

            return desc;
        }

        [TestMethod]
        [DataRow(ZUGFeRDVersion.Version1, Profile.Basic,      @"zugferd10\Schema\ZUGFeRD1p0.xsd")]
        [DataRow(ZUGFeRDVersion.Version1, Profile.Comfort,    @"zugferd10\Schema\ZUGFeRD1p0.xsd")]
        [DataRow(ZUGFeRDVersion.Version1, Profile.Extended,   @"zugferd10\Schema\ZUGFeRD1p0.xsd")]

        [DataRow(ZUGFeRDVersion.Version20, Profile.Minimum,   @"zugferd20\Schema\BASIC und MINIMUM\zugferd2p0_basicwl_minimum.xsd")]
        [DataRow(ZUGFeRDVersion.Version20, Profile.Basic,     @"zugferd20\Schema\BASIC und MINIMUM\zugferd2p0_basicwl_minimum.xsd")]
        [DataRow(ZUGFeRDVersion.Version20, Profile.BasicWL,   @"zugferd20\Schema\BASIC und MINIMUM\zugferd2p0_basicwl_minimum.xsd")]
        [DataRow(ZUGFeRDVersion.Version20, Profile.Comfort,   @"zugferd20\Schema\EN16931\zugferd2p0_en16931.xsd")]
        [DataRow(ZUGFeRDVersion.Version20, Profile.Extended,  @"zugferd20\Schema\EXTENDED\zugferd2p0_extended.xsd")]

        [DataRow(ZUGFeRDVersion.Version23, Profile.Minimum,   @"zugferd23de\Schema\0. Factur-X_1.07.2_MINIMUM\Factur-X_1.07.2_MINIMUM.xsd")]
        [DataRow(ZUGFeRDVersion.Version23, Profile.Basic,     @"zugferd23de\Schema\2. Factur-X_1.07.2_BASIC\Factur-X_1.07.2_BASIC.xsd")]
        [DataRow(ZUGFeRDVersion.Version23, Profile.BasicWL,   @"zugferd23de\Schema\1. Factur-X_1.07.2_BASICWL\Factur-X_1.07.2_BASICWL.xsd")]
        [DataRow(ZUGFeRDVersion.Version23, Profile.Comfort,   @"zugferd23de\Schema\3. Factur-X_1.07.2_EN16931\Factur-X_1.07.2_EN16931.xsd")]
        [DataRow(ZUGFeRDVersion.Version23, Profile.XRechnung, @"zugferd23de\Schema\3. Factur-X_1.07.2_EN16931\Factur-X_1.07.2_EN16931.xsd")]
        [DataRow(ZUGFeRDVersion.Version23, Profile.Extended,  @"zugferd23de\Schema\4. Factur-X_1.07.2_EXTENDED\Factur-X_1.07.2_EXTENDED.xsd")]
        public void ValidateCIIXmlSchema(ZUGFeRDVersion version, Profile profile, string schemaFile)
        {
            InvoiceDescriptor desc = CreateInvoiceDescriptor();
            MemoryStream ms = new MemoryStream();

            desc.Save(ms, version, profile, ZUGFeRDFormats.CII);
            ms.Seek(0, SeekOrigin.Begin);

            string path =  _makeSurePathIsCrossPlatformCompatible(Path.Combine(@"..\..\..\..\documentation\", Path.GetDirectoryName(schemaFile)));

            List<ValidationEventArgs> validationResults = Validate(ms, path);

            List<string> errors = FormatErrors(validationResults, ms);

            if (errors.Count > 0)
                Assert.Fail(string.Join(Environment.NewLine + "-------------" + Environment.NewLine, errors));
        }

        [TestMethod]
        [DataRow(ZUGFeRDVersion.Version23, Profile.XRechnung, InvoiceType.Invoice)]
        [DataRow(ZUGFeRDVersion.Version23, Profile.XRechnung, InvoiceType.CreditNote)]
        public void ValidateUBLXmlSchema(ZUGFeRDVersion version, Profile profile, InvoiceType invoiceType)
        {
            InvoiceDescriptor desc = CreateInvoiceDescriptor();

            desc.Type = invoiceType;

            MemoryStream ms = new MemoryStream();

            desc.Save(ms, version, profile, ZUGFeRDFormats.UBL);
            ms.Seek(0, SeekOrigin.Begin);

            string path = _makeSurePathIsCrossPlatformCompatible(@"..\..\..\..\documentation\xRechnung\XRechnung 3.0.1\validator-configuration-xrechnung_3.0.1_2023-09-22\resources\ubl\2.1\xsd");

            List<ValidationEventArgs> validationResults = Validate(ms, path);

            List<string> errors = FormatErrors(validationResults, ms);

            if (errors.Count > 0)
                Assert.Fail(string.Join(Environment.NewLine + "-------------" + Environment.NewLine, errors));
        }
    }
}
