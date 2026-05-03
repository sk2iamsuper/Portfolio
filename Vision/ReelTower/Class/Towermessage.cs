using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace ReelTW.TowerCommunication
{
    public enum TowerResult
    {
        OK,
        NG,
        ERROR
    }

    public sealed class TowerReelItem
    {
        public string Id { get; set; }
        public string PartNumber { get; set; }
        public TowerResult? Result { get; set; }
        public string ErrorId { get; set; }
        public string ErrorDesc { get; set; }
    }

    public sealed class TowerBoxItem
    {
        public string Id { get; set; }
        public string Location { get; set; }
        public TowerResult? Result { get; set; }
        public string ErrorId { get; set; }
        public string ErrorDesc { get; set; }
    }

    public sealed class TowerMessage
    {
        public string CommunicationId { get; set; }
        public string Name { get; set; }
        public string RequestId { get; set; }
        public string BaseId { get; set; }
        public string BarcodeType { get; set; }
        public string Retry { get; set; }
        public string EquipmentId { get; set; }
        public string SlotId { get; set; }
        public string WorkOrderId { get; set; }
        public string AssyPartNumberId { get; set; }
        public string LineId { get; set; }
        public string WorkFaceId { get; set; }
        public string McId { get; set; }
        public TowerResult? Result { get; set; }
        public string ErrorId { get; set; }
        public string ErrorDesc { get; set; }
        public string Msl { get; set; }
        public string BoxId { get; set; }
        public string Urgent { get; set; }
        public List<TowerReelItem> Reels { get; private set; }
        public List<TowerBoxItem> Boxes { get; private set; }
        public XmlDocument RawDocument { get; private set; }

        public TowerMessage()
        {
            Reels = new List<TowerReelItem>();
            Boxes = new List<TowerBoxItem>();
        }

        public static string NewRequestId()
        {
            // 사양서 기준 REQUESTID는 타임스탬프 형태로 메시지 추적에 사용한다.
            return DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        }

        public static TowerMessage Parse(string xml)
        {
            // 타워 IF는 REELSYS XML을 기준으로 수신 메시지를 공통 모델로 변환한다.
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);

            XmlElement root = document.DocumentElement;
            if (root == null || !String.Equals(root.Name, "REELSYS", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid tower message. Root element must be REELSYS.");

            TowerMessage message = new TowerMessage();
            message.RawDocument = document;
            message.CommunicationId = root.GetAttribute("ID");
            message.Name = root.GetAttribute("NAME");
            message.RequestId = GetText(root, "REQUESTID");
            message.BaseId = GetText(root, "BASEID");
            message.BarcodeType = GetText(root, "BARCODETYPE");
            message.Retry = GetText(root, "RETRY");
            message.EquipmentId = GetText(root, "EQUIPID");
            message.SlotId = GetText(root, "SLOTID");
            message.WorkOrderId = GetText(root, "WOID");
            message.AssyPartNumberId = GetText(root, "PNID");
            message.LineId = GetText(root, "LINEID");
            message.WorkFaceId = GetText(root, "WFID");
            message.McId = GetText(root, "MCID");
            message.BoxId = GetText(root, "BOXID");
            message.Urgent = GetText(root, "URGENT");
            message.ErrorId = GetText(root, "ERRORID");
            message.ErrorDesc = GetText(root, "ERRORDESC");
            message.Msl = GetText(root, "MSL");
            message.Result = ParseResult(GetText(root, "RESULT"));

            // REELID는 투입/배출 명령에서 복수로 올 수 있어 리스트로 보관한다.
            XmlNodeList reelNodes = root.GetElementsByTagName("REELID");
            foreach (XmlElement reelNode in reelNodes)
            {
                TowerReelItem reel = new TowerReelItem();
                reel.Id = GetText(reelNode, "ID");
                reel.PartNumber = GetText(reelNode, "PN");
                reel.Result = ParseResult(GetText(reelNode, "RESULT"));
                reel.ErrorId = GetText(reelNode, "ERRORID");
                reel.ErrorDesc = GetText(reelNode, "ERRORDESC");
                message.Reels.Add(reel);
            }

            // BOXID는 Reel Box 배출 완료 확인에서 위치/결과 판단에 사용한다.
            XmlNodeList boxNodes = root.GetElementsByTagName("BOXID");
            foreach (XmlElement boxNode in boxNodes)
            {
                TowerBoxItem box = new TowerBoxItem();
                box.Id = GetText(boxNode, "ID");
                box.Location = GetText(boxNode, "LOC");
                box.Result = ParseResult(GetText(boxNode, "RESULT"));
                box.ErrorId = GetText(boxNode, "ERRORID");
                box.ErrorDesc = GetText(boxNode, "ERRORDESC");
                message.Boxes.Add(box);
            }

            return message;
        }

        internal static TowerResult? ParseResult(string value)
        {
            // 결과값은 OK/NG/ERROR 외 값이 들어오면 미확정 상태로 둔다.
            if (String.IsNullOrWhiteSpace(value))
                return null;

            TowerResult result;
            if (Enum.TryParse(value.Trim(), true, out result))
                return result;

            return null;
        }

        internal static string ResultToString(TowerResult? result)
        {
            return result.HasValue ? result.Value.ToString().ToUpperInvariant() : String.Empty;
        }

        internal static string GetText(XmlElement element, string tagName)
        {
            // 중첩된 동일 태그를 잘못 읽지 않도록 현재 요소의 직계 자식만 조회한다.
            foreach (XmlNode node in element.ChildNodes)
            {
                XmlElement child = node as XmlElement;
                if (child != null && String.Equals(child.Name, tagName, StringComparison.OrdinalIgnoreCase))
                    return child.InnerText.Trim();
            }

            return String.Empty;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Name);
            builder.Append(" / RequestId=");
            builder.Append(RequestId);
            builder.Append(" / Result=");
            builder.Append(ResultToString(Result));
            return builder.ToString();
        }
    }
}
