using System.Diagnostics;
using QBFC16Lib;
using QB_ServiceBills_Lib;
using static QB_ServiceBills_Test.CommonMethods;

namespace QB_ServiceBills_Test
{
    [Collection("Sequential Tests")]
    public class ServiceBillAdderTests
    {
        [Fact]
        public void AddServiceBill_ToQuickBooks_Succeeds()
        {
            string vendorListID = "";
            string vendorName = "TestVendor_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            int companyID = 123;
            string invoiceNum = "INV_" + Guid.NewGuid().ToString("N").Substring(0, 5);
            DateTime billDate = DateTime.Today;

            string expAcct1 = "Utilities";
            double amt1 = 45.0;
            string expAcct2 = "Computer and Internet Expenses";
            double amt2 = 20.0;

            string txnID = "";

            try
            {
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    // Add vendor
                    vendorListID = AddVendor(qbSession, vendorName);

                    // Add bill
                    txnID = AddServiceBill(
                        qbSession,
                        vendorListID,
                        vendorName,
                        billDate,
                        invoiceNum,
                        companyID,
                        expAcct1,
                        amt1,
                        expAcct2,
                        amt2
                    );
                }

                // Query back
                var serviceBills = ServiceBillReader.QueryAllServiceBills();
                var serviceBill = serviceBills.FirstOrDefault(x =>
                    x.InvoiceNum == invoiceNum &&
                    x.Memo == companyID.ToString()
                );

                Assert.NotNull(serviceBill);
                Assert.Equal(vendorName, serviceBill.VendorName);
                Assert.Equal(companyID.ToString(), serviceBill.Memo);
                Assert.Equal(billDate.Date, serviceBill.BillDate.Date);
                Assert.Equal(invoiceNum, serviceBill.InvoiceNum);
                Assert.Equal(2, serviceBill.Lines.Count);

                Assert.Equal(expAcct1, serviceBill.Lines[0].AccountName);
                Assert.Equal(amt1, serviceBill.Lines[0].Amount);
                Assert.Equal(expAcct2, serviceBill.Lines[1].AccountName);
                Assert.Equal(amt2, serviceBill.Lines[1].Amount);
            }
            finally
            {
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    if (!string.IsNullOrEmpty(txnID))
                        DeleteBill(qbSession, txnID);

                    if (!string.IsNullOrEmpty(vendorListID))
                        DeleteListObject(qbSession, vendorListID, ENListDelType.ldtVendor);
                }
            }
        }

        // ---------------------- Helper Methods ----------------------

        private string AddVendor(QuickBooksSession qbSession, string vendorName)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IVendorAdd vendAdd = request.AppendVendorAddRq();

            vendAdd.Name.SetValue(vendorName);

            var resp = qbSession.SendRequest(request);
            return ExtractVendorListID(resp);
        }

        private string AddServiceBill(
            QuickBooksSession qbSession,
            string vendorListID,
            string vendorName,
            DateTime billDate,
            string vendorInvoiceNum,
            int companyID,
            string expAcct1,
            double amt1,
            string expAcct2,
            double amt2
        )
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IBillAdd billAddRq = request.AppendBillAddRq();

            billAddRq.VendorRef.ListID.SetValue(vendorListID);
            billAddRq.TxnDate.SetValue(billDate);
            billAddRq.RefNumber.SetValue(vendorInvoiceNum);
            billAddRq.Memo.SetValue(companyID.ToString());

            var expLine1 = billAddRq.ExpenseLineAddList.Append();
            expLine1.AccountRef.FullName.SetValue(expAcct1);
            expLine1.Amount.SetValue(amt1);

            var expLine2 = billAddRq.ExpenseLineAddList.Append();
            expLine2.AccountRef.FullName.SetValue(expAcct2);
            expLine2.Amount.SetValue(amt2);

            var resp = qbSession.SendRequest(request);
            return ExtractBillTxnID(resp);
        }

        private void DeleteBill(QuickBooksSession qbSession, string txnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var delRq = request.AppendTxnDelRq();
            delRq.TxnDelType.SetValue(ENTxnDelType.tdtBill);
            delRq.TxnID.SetValue(txnID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting ServiceBill TxnID={txnID}");
        }

        private void DeleteListObject(QuickBooksSession qbSession, string listID, ENListDelType listDelType)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var listDel = request.AppendListDelRq();
            listDel.ListDelType.SetValue(listDelType);
            listDel.ListID.SetValue(listID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting {listDelType} {listID}");
        }

        private string ExtractVendorListID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from VendorAdd.");

            var firstResp = list.GetAt(0);
            if (firstResp.StatusCode != 0)
                throw new Exception($"VendorAdd failed: {firstResp.StatusMessage}");

            var vendRet = firstResp.Detail as IVendorRet;
            if (vendRet == null)
                throw new Exception("No IVendorRet returned.");

            return vendRet.ListID.GetValue();
        }

        private string ExtractBillTxnID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from BillAdd.");

            var firstResp = list.GetAt(0);
            if (firstResp.StatusCode != 0)
                throw new Exception($"BillAdd failed: {firstResp.StatusMessage}");

            var billRet = firstResp.Detail as IBillRet;
            if (billRet == null)
                throw new Exception("No IBillRet returned.");

            return billRet.TxnID.GetValue();
        }

        private void CheckForError(IMsgSetResponse resp, string context)
        {
            if (resp?.ResponseList == null || resp.ResponseList.Count == 0)
                return;

            var firstResp = resp.ResponseList.GetAt(0);
            if (firstResp.StatusCode != 0)
            {
                throw new Exception($"Error {context}: {firstResp.StatusMessage}. Status code: {firstResp.StatusCode}");
            }
            else
            {
                Debug.WriteLine($"OK: {context}");
            }
        }
    }
}
