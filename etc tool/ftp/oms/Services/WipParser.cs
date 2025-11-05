using System;
using tcp_socket.Models;

namespace tcp_socket.Services
{
    public class WipParser
    {
        // Parse one fixed-width record line into model. Defensive - checks length before substring.
        public CMWipInf Parse(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;

            // Ensure row long enough by padding with spaces to avoid exceptions
            if (row.Length < 1500) row = row.PadRight(1500);

            var m = new CMWipInf();

            // NOTE: Indices are taken from original code. Adjust if source format changes.
            m.StartTag = row.Substring(0, 3).Trim();
            m.CompanyCode = row.Substring(3, 2).Trim();
            m.ProductCode = row.Substring(5, 30).Trim();
            m.RunId = row.Substring(35, 15).Trim();
            m.LotId = row.Substring(50, 15).Trim();
            m.LotType = row.Substring(65, 2).Trim();
            m.ReturnType = row.Substring(67, 2).Trim();
            m.ProcessId = row.Substring(69, 15).Trim();
            m.StepId = row.Substring(84, 30).Trim();
            m.StepSeqNo = row.Substring(114, 16).Trim();
            m.StepDesc = row.Substring(130, 24).Trim();
            // Original had two different offsets for STEP_IN_DTTM; choose 159 as in refactor
            m.StepInDttm = row.Substring(159, 14).Trim();
            m.AreaFlag = row.Substring(173, 4).Trim();
            m.AreaId = row.Substring(177, 4).Trim();
            m.ChipQty = row.Substring(181, 10).Trim();
            m.WaferQty = row.Substring(191, 10).Trim();
            m.HoldFlag = row.Substring(202, 2).Trim();
            m.HoldCode = row.Substring(204, 10).Trim();
            m.HoldDttm = row.Substring(214, 14).Trim();
            m.NcfCode = row.Substring(228, 20).Trim();
            m.NcaCode = row.Substring(248, 20).Trim();
            m.NctCode = row.Substring(268, 20).Trim();
            m.NcqCode = row.Substring(288, 20).Trim();
            m.Other = row.Substring(308, 20).Trim();
            m.LossQty = row.Substring(328, 10).Trim();
            m.BonusQty = row.Substring(338, 10).Trim();
            m.FabLine = row.Substring(348, 1).Trim();
            m.CreateDttm = row.Substring(414, 14).Trim();
            m.CutoffDate = row.Substring(428, 8).Trim();
            m.Inkless = row.Substring(436, 1024).Trim();

            return m;
        }
    }
}
