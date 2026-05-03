using System;
using System.Collections.Generic;
using System.Xml;

namespace ReelTW.TowerCommunication
{
    public sealed class TowerReplyContext
    {
        public string CommunicationId { get; set; }
        public string RequestId { get; set; }
        public string ReelId { get; set; }
        public string PartNumber { get; set; }
        public string BarcodeType { get; set; }
        public string Msl { get; set; }
        public string BoxId { get; set; }
        public TowerResult Result { get; set; }
        public string ErrorId { get; set; }
        public string ErrorDesc { get; set; }

        public TowerReplyContext()
        {
            CommunicationId = "XXX";
            RequestId = TowerMessage.NewRequestId();
            Result = TowerResult.OK;
        }
    }

    public static class TowerMessageFactory
    {
        public static string CreateReelLoadCheckReply(TowerMessage request, TowerReplyContext context)
        {
            // Reel ID 조회 응답은 Reel/PN/MSL/바코드 타입을 포함해 라벨 발행 판단에 사용된다.
            XmlDocument document = CreateDocument(request, "REELLOADCHECK", context);
            XmlElement root = document.DocumentElement;
            XmlElement reel = Append(document, root, "REELID", null);
            Append(document, reel, "ID", First(context.ReelId, FirstReelId(request)));
            Append(document, reel, "PN", context.PartNumber);
            Append(document, root, "RESULT", TowerMessage.ResultToString(context.Result));
            Append(document, root, "ERRORID", context.ErrorId);
            Append(document, root, "ERRORDESC", context.ErrorDesc);
            Append(document, root, "MSL", context.Msl);
            Append(document, root, "BARCODETYPE", First(context.BarcodeType, request.BarcodeType));
            AppendRequestId(document, root, request, context);
            return ToXml(document);
        }

        public static string CreateSimpleResultReply(TowerMessage request, string name, TowerReplyContext context)
        {
            // 단순 승인/확인 계열 명령은 원 요청의 REQUESTID를 유지하고 결과만 응답한다.
            XmlDocument document = CreateDocument(request, name, context);
            XmlElement root = document.DocumentElement;
            if (!String.IsNullOrEmpty(FirstReelId(request)) || !String.IsNullOrEmpty(context.ReelId))
            {
                XmlElement reel = Append(document, root, "REELID", null);
                Append(document, reel, "ID", First(context.ReelId, FirstReelId(request)));
                Append(document, reel, "PN", First(context.PartNumber, FirstPartNumber(request)));
            }
            Append(document, root, "RESULT", TowerMessage.ResultToString(context.Result));
            Append(document, root, "ERRORID", context.ErrorId);
            Append(document, root, "ERRORDESC", context.ErrorDesc);
            AppendRequestId(document, root, request, context);
            return ToXml(document);
        }

        public static string CreateUnloadOutConfirm(TowerMessage request, TowerReplyContext context)
        {
            // Reel 배출 요청은 여러 Reel을 한 번에 확인할 수 있어 항목별 결과를 만든다.
            XmlDocument document = CreateDocument(request, "REELUNLOADOUT", context);
            XmlElement root = document.DocumentElement;
            XmlElement list = Append(document, root, "SHIPREELLIST", null);

            IList<TowerReelItem> reels = request.Reels.Count > 0 ? request.Reels : new List<TowerReelItem>();
            if (reels.Count == 0)
                reels.Add(new TowerReelItem { Id = context.ReelId, PartNumber = context.PartNumber });

            foreach (TowerReelItem item in reels)
            {
                XmlElement reel = Append(document, list, "REELID", null);
                Append(document, reel, "ID", item.Id);
                Append(document, reel, "PN", item.PartNumber);
                Append(document, reel, "RESULT", TowerMessage.ResultToString(item.Result ?? context.Result));
                Append(document, reel, "ERRORID", First(item.ErrorId, context.ErrorId));
                Append(document, reel, "ERRORDESC", First(item.ErrorDesc, context.ErrorDesc));
            }

            Append(document, root, "RESULT", TowerMessage.ResultToString(context.Result));
            Append(document, root, "ERRORID", context.ErrorId);
            Append(document, root, "ERRORDESC", context.ErrorDesc);
            AppendRequestId(document, root, request, context);
            return ToXml(document);
        }

        public static string CreateBoxUnloadDoneReply(TowerMessage request, TowerReplyContext context)
        {
            // Box 배출 완료 응답은 Reel 목록과 Box 위치/결과를 함께 반환한다.
            XmlDocument document = CreateDocument(request, "REELUNLOADDONE", context);
            XmlElement root = document.DocumentElement;
            XmlElement list = Append(document, root, "OUTREELLIST", null);

            IList<TowerReelItem> reels = request.Reels.Count > 0 ? request.Reels : new List<TowerReelItem>();
            foreach (TowerReelItem item in reels)
            {
                XmlElement reel = Append(document, list, "REELID", null);
                Append(document, reel, "ID", item.Id);
                Append(document, reel, "PN", item.PartNumber);
                Append(document, reel, "RESULT", TowerMessage.ResultToString(item.Result ?? context.Result));
                Append(document, reel, "ERRORID", First(item.ErrorId, context.ErrorId));
                Append(document, reel, "ERRORDESC", First(item.ErrorDesc, context.ErrorDesc));
                Append(document, reel, "WOID", request.WorkOrderId);
                Append(document, reel, "PNID", request.AssyPartNumberId);
                Append(document, reel, "LINEID", request.LineId);
                Append(document, reel, "WFID", request.WorkFaceId);
                Append(document, reel, "MCID", request.McId);
            }

            XmlElement box = Append(document, root, "BOXID", null);
            Append(document, box, "ID", First(context.BoxId, request.BoxId));
            Append(document, box, "LOC", String.Empty);
            Append(document, box, "RESULT", TowerMessage.ResultToString(context.Result));
            Append(document, box, "ERRORID", context.ErrorId);
            Append(document, box, "ERRORDESC", context.ErrorDesc);
            AppendRequestId(document, root, request, context);
            return ToXml(document);
        }

        private static XmlDocument CreateDocument(TowerMessage request, string name, TowerReplyContext context)
        {
            // 모든 응답은 REELSYS 루트와 NAME/ID 속성을 공통 헤더로 사용한다.
            XmlDocument document = new XmlDocument();
            XmlElement root = document.CreateElement("REELSYS");
            root.SetAttribute("ID", First(context.CommunicationId, request.CommunicationId));
            root.SetAttribute("NAME", name);
            document.AppendChild(root);
            return document;
        }

        private static void AppendRequestId(XmlDocument document, XmlElement root, TowerMessage request, TowerReplyContext context)
        {
            // 응답 REQUESTID는 요청과 동일하게 유지해 송수신 매칭이 가능하게 한다.
            Append(document, root, "REQUESTID", First(request.RequestId, context.RequestId));
        }

        private static XmlElement Append(XmlDocument document, XmlElement parent, string name, string value)
        {
            XmlElement element = document.CreateElement(name);
            if (value != null)
                element.InnerText = value;
            parent.AppendChild(element);
            return element;
        }

        private static string ToXml(XmlDocument document)
        {
            return document.OuterXml;
        }

        private static string First(string value, string fallback)
        {
            return String.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string FirstReelId(TowerMessage request)
        {
            return request.Reels.Count > 0 ? request.Reels[0].Id : String.Empty;
        }

        private static string FirstPartNumber(TowerMessage request)
        {
            return request.Reels.Count > 0 ? request.Reels[0].PartNumber : String.Empty;
        }
    }
}
