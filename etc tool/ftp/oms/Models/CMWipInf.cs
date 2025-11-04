namespace tcp_socket.Models
{
// Simple POCO representing selected fields from original fixed-width record.
public class CMWipInf
{
public string StartTag { get; set; }
public string CompanyCode { get; set; }
public string ProductCode { get; set; }
public string RunId { get; set; }
public string LotId { get; set; }
public string LotType { get; set; }
public string ReturnType { get; set; }
public string ProcessId { get; set; }
public string StepId { get; set; }
public string StepSeqNo { get; set; }
public string StepDesc { get; set; }
public string StepInDttm { get; set; }
public string AreaFlag { get; set; }
public string AreaId { get; set; }
public string ChipQty { get; set; }
public string WaferQty { get; set; }
public string HoldFlag { get; set; }
public string HoldCode { get; set; }
public string HoldDttm { get; set; }
public string NcfCode { get; set; }
public string NcaCode { get; set; }
public string NctCode { get; set; }
public string NcqCode { get; set; }
public string Other { get; set; }
public string LossQty { get; set; }
public string BonusQty { get; set; }
public string FabLine { get; set; }
public string CreateDttm { get; set; }
public string CutoffDate { get; set; }
public string Inkless { get; set; }
}
