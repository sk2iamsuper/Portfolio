using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;

using System.IO;
using System.Net;
using System.Net.Sockets;

using System.Diagnostics;
using System.Threading;

namespace ReelTW
{
    public class ClassMelsecPLC
    {
        public struct MelescTCp
        {
            public string[] strAdress;
            public string[] strBitInStartAddress;
            public string[] strBitOutStartAddress;
            public string[] strBitOutStartAddress1;
            public string[] strWordInStartAddress;
            public string[] strWordASCIIInStartAddress;
            public string[] strWordASCIIInStartAddress1;
            
            public string[] strWordOutStartAddress_X1;
            public string[] strWordOutStartAddress_X2;
            public string[] strWordOutStartAddress_Y1;
            public string[] strWordOutStartAddress_Y2;

            public int[] iPortNo;
            public int[] iBitInSize;
            public int[] iBitOutSize;
            public int[] iWordASCIISize;
            public int[] iWordASCIISize1;
            public int[] iWordInSize;
            public int[] iWordOutSizeX1;
            public int[] iWordOutSizeY1;
            public int[] iWordOutSizeX2;
            public int[] iWordOutSizeY2;
            public int iPLCType;
            public int iProtocoltype;

            public void Initialize(int iPLCCount)
            {
                strAdress = new string[iPLCCount];
                strBitInStartAddress        = new string[iPLCCount];
                strBitOutStartAddress       = new string[iPLCCount];
                strWordInStartAddress       = new string[iPLCCount];
                strWordOutStartAddress_X1   = new string[iPLCCount];
                strWordOutStartAddress_X2   = new string[iPLCCount];
                strWordOutStartAddress_Y1   = new string[iPLCCount];
                strWordOutStartAddress_Y2   = new string[iPLCCount];
                strWordASCIIInStartAddress  = new string[iPLCCount];
                strWordASCIIInStartAddress1 = new string[iPLCCount];

                iPLCType          = new int();
                iProtocoltype     = new int();

                iBitInSize        = new int[iPLCCount];
                iBitOutSize       = new int[iPLCCount];
                iWordInSize       = new int[iPLCCount];
                iWordOutSizeX1    = new int[iPLCCount];
                iWordOutSizeY1    = new int[iPLCCount];
                iWordOutSizeX2    = new int[iPLCCount];
                iWordOutSizeY2    = new int[iPLCCount];
                iWordASCIISize    = new int[iPLCCount];
                iWordASCIISize1   = new int[iPLCCount];
                iPortNo           = new int[iPLCCount];                
                
            }

        }

        public string strASCIICode = ""; 
        //public FormBase objFormBase;
     
        public Socket[] objPLC_Client;
        public IPAddress[] objIpAddress;
        public IPEndPoint[] objPLC_IPEndPoint;
       
        public MelescTCp objMelescTcp;
    
        public int iPLCCount;
        
        private int iNo;


        private string strReciveData;

        
        private delegate void delEventTcpRecieve(object sender, string msg);
        protected object objPLCLockObject;
        protected object LockPLCSendObject;
        protected object LockPLCRecieveObject;

        public bool IsConnected(int plcNo = 0)
        {
            // Main/FormPLC에서 PLC 소켓 상태를 직접 확인할 수 있게 하는 토대 메서드다.
            return objPLC_Client != null &&
                   plcNo >= 0 &&
                   plcNo < objPLC_Client.Length &&
                   objPLC_Client[plcNo] != null &&
                   objPLC_Client[plcNo].Connected;
        }

      /******************************************************************************************************************************************************************
      * 제목 : 초기화
      * 인자 : 
      * 리턴 : 
      * 설명 : 
      ******************************************************************************************************************************************************************/
        public bool Initialize(string IP_PLC, int iPortPLC)
        {
            // Melsec TCP 3E 프레임 통신을 위한 IP/Port와 소켓 배열을 구성한다.
            bool bReturn =false;
            do 
            {
                iPLCCount = 1;
                objMelescTcp = new MelescTCp();
                objMelescTcp.Initialize(iPLCCount);
                objMelescTcp.iPLCType = 0;
                objMelescTcp.iProtocoltype = 0;
                for (int iLoopCount = 0; iLoopCount < iPLCCount; iLoopCount++)
                {
                    objMelescTcp.strAdress[iLoopCount] = IP_PLC;
                    objMelescTcp.iPortNo[iLoopCount] = iPortPLC;
                }

               
               objPLC_Client = new Socket[iPLCCount];
               objPLC_IPEndPoint = new IPEndPoint[iPLCCount];
               LockPLCSendObject = new object();
               objPLCLockObject = new object();
               LockPLCRecieveObject = new object(); 

               objIpAddress = new IPAddress[iPLCCount];
               
               for(int iLoopCount = 0; iLoopCount < iPLCCount; iLoopCount++)
               {                  
                   objIpAddress[iLoopCount] = IPAddress.Parse(objMelescTcp.strAdress[iLoopCount]);
                   objPLC_IPEndPoint[iLoopCount] = new IPEndPoint(objIpAddress[iLoopCount], objMelescTcp.iPortNo[iLoopCount]);
                   objPLC_Client[iLoopCount] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                
                   Connect(iLoopCount);
               }

              bReturn = true;
             
            } while (false);
            return bReturn;
        }

       /******************************************************************************************************************************************************************
       * 제목 : 소켓 연결
       * 인자 : 
       * 리턴 : 
       * 설명 : 
       ******************************************************************************************************************************************************************/
        public void Connect(int iCount)
        {
            try
            {
                // PLC별 EndPoint에 TCP 소켓을 연결한다.
                objPLC_Client[iCount].Connect(objPLC_IPEndPoint[iCount]);
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message); ;
            }
            
        }
        /******************************************************************************************************************************************************************
        * 제목 : 해제
        * 인자 : 
        * 리턴 : 
        * 설명 : 
        ******************************************************************************************************************************************************************/
        public void DeInitialize()
        {
            DisConnect();
        }
        /******************************************************************************************************************************************************************
       * 제목 : 소켓 연결 해제
       * 인자 : 
       * 리턴 : 
       * 설명 : 
       ******************************************************************************************************************************************************************/
        public void DisConnect()
        {
            // 생성된 PLC 소켓을 모두 닫아 통신 자원을 해제한다.
            for (int iLoopCount = 0; iLoopCount < iPLCCount; iLoopCount++)
            {
                objPLC_Client[iLoopCount].Close();
            }
            
        }
        /******************************************************************************************************************************************************************
         * 제목 : TCP 파라메터 로드
         * 인자 : 
         * 리턴 : 
         * 설명 : 
         ******************************************************************************************************************************************************************/
        /******************************************************************************************************************************************************************
        * 제목 : ReadComand
        * 인자 : 
        * 리턴 : 
        * 설명 : 
        ******************************************************************************************************************************************************************/
        private string ReadCommand(int iPLC_No, string strAddress, int iCount)
        {
            // PLC Device 주소와 Word/Bit 개수를 Melsec Read 프레임 문자열로 조립한다.
         
            string strBitWord;
            string strReadComand;
            strReadComand = "";
            string strAddressDM = strAddress.Substring(0,1);
            string strAdddressNo =  strAddress.Remove(0, 1);
            string strAdddressNo_H, strAdddressNo_M, strAdddressNo_L;
            string strDeviceCount = string.Format("{0:X4}", iCount);
            

            if (0 == objMelescTcp.iProtocoltype)
            {
                strAdddressNo = String.Format("{0:X6}", Convert.ToInt16(strAdddressNo));
                strAdddressNo_H =strAdddressNo.Substring(4, 2);
                strAdddressNo_M = strAdddressNo.Substring(2, 2);
                strAdddressNo_L =strAdddressNo.Substring(0, 2); //Hex Adress H + L
                strAdddressNo = strAdddressNo_H + strAdddressNo_M + strAdddressNo_L;
                strDeviceCount = strDeviceCount.Substring(2, 2) + strDeviceCount.Substring(0, 2);


                if ("M" == strAddressDM)
                {
                    strAddressDM = "90";
                    strBitWord = "0100";              
                }
                else
                {
                    strAddressDM = "A8";
                    strBitWord = "0000";
                }

                strReadComand = strReadComand + "5000";                         // 서브헤더
                strReadComand = strReadComand + "00";                           // Network No
                strReadComand = strReadComand + "FF";                           // PC No
                strReadComand = strReadComand + "FF03";                         // 요구 상대 모듈 I/O No
                strReadComand = strReadComand + "00";                           // 요구 상대 국번호
                strReadComand = strReadComand + "0C00";                         // 요구 데이터 길이 0C18[3098 LEN], 0018[요구 데이터 길이 뒤 부터 24BYTE :09~21 12*2]
                strReadComand = strReadComand + "1000";                         // CPU 감시 타이머
                strReadComand = strReadComand + "0104";                         // 0401[READ], 1401[WRITE]
                strReadComand = strReadComand + strBitWord;                     // 0000[WORD], 0001[BIT]
                strReadComand = strReadComand + strAdddressNo;                  // Device Address Hexcode L88 H13 자릿수00
                strReadComand = strReadComand + strAddressDM;                   // DEVICE ADDRESS  //Binary Mode D*:A8 M*:90
                strReadComand = strReadComand + strDeviceCount;                 // DEVICE 점수 L05 H00
            }
            else
            {
                if ("M" == strAddressDM)
                {
                    strBitWord = "0001";
                }
                else
                {
                    strBitWord = "0000";
                }
                strReadComand = strReadComand + "5000";                                   // 서브헤더
                strReadComand = strReadComand + "00";                                     // Network No
                strReadComand = strReadComand + "FF";                                     // PC No
                strReadComand = strReadComand + "03FF";                                   // 요구 상대 모듈 I/O No
                strReadComand = strReadComand + "00";                                     // 요구 상대 국번호
                strReadComand = strReadComand + "0018";                                   // 요구 데이터 길이 0C18[3098 LEN], 0018[요구 데이터 길이 뒤 부터 24BYTE :09~21 12*2]
                strReadComand = strReadComand + "0010";                                   // CPU 감시 타이머
                strReadComand = strReadComand + "0401";                                   // 0401[READ], 1401[WRITE]
                strReadComand = strReadComand + strBitWord;                               // 0000[WORD], 0001[BIT]
                strReadComand = strReadComand + strAddressDM + "*";                       // DEVICE CODE
                strReadComand = strReadComand + String.Format("{0:D6}", strAdddressNo);   // DEVICE ADDRESS
                strReadComand = strReadComand + String.Format("{0:D4}", iCount);          // DEVICE 점수
             
            }
            return strReadComand;
        }
        /******************************************************************************************************************************************************************
      * 제목 : WriteComand
      * 인자 : 
      * 리턴 : 
      * 설명 : 
      ******************************************************************************************************************************************************************/
        public string WriteCommand(int iPLC_No, string strAddress, int iCount)
        {
            // PLC Device 주소와 데이터 개수를 Melsec Write 프레임 문자열로 조립한다.
      
            int iLenth;
            string strBitWord;
            string strReadComand;
            strReadComand = "";
            string strAddressDM = strAddress.Substring(0, 1);
            string strAdddressNo = strAddress.Remove(0, 1);
            string strAdddressNo_H, strAdddressNo_M, strAdddressNo_L;
            string strHexLenth, strHexLenth_HL;
            string strDeviceCount, strDeviceCount_HL;
           

            if (0 == objMelescTcp.iProtocoltype)
            {
                strAdddressNo = String.Format("{0:X6}", strAdddressNo);
                strAdddressNo = String.Format("{0:X6}", Convert.ToInt16(strAdddressNo));
                strAdddressNo_H = strAdddressNo.Substring(4, 2);
                strAdddressNo_M = strAdddressNo.Substring(2, 2);
                strAdddressNo_L = strAdddressNo.Substring(0, 2); //Hex Adress H + L
                strAdddressNo = strAdddressNo_H + strAdddressNo_M + strAdddressNo_L;
                

                if ("M" == strAddressDM)
                {
                    strAddressDM = "90";
                    strBitWord = "0100";
                    iLenth = 12 + iCount;
                    strHexLenth = string.Format("{0:X4}", iLenth);
                    strHexLenth_HL = strHexLenth.Substring(2, 2) + strHexLenth.Substring(0, 2);
                    strDeviceCount = string.Format("{0:D4}", iCount);
                }
                else
                {
                    strAddressDM = "A8";
                    strBitWord = "0000";
                    strDeviceCount = String.Format("{0:X4}",(iCount*1));
                    iLenth = 12 + (iCount * 2);
                    strHexLenth = string.Format("{0:X4}", iLenth);
                    strHexLenth_HL = strHexLenth.Substring(2, 2) + strHexLenth.Substring(0, 2);
                }
                strDeviceCount_HL = strDeviceCount.Substring(2, 2) + strDeviceCount.Substring(0, 2);                 
                strReadComand = strReadComand + "5000";                                                              // 서브헤더
                strReadComand = strReadComand + "00";                                                                // Network No
                strReadComand = strReadComand + "FF";                                                                // PC No
                strReadComand = strReadComand + "FF03";                                                              // 요구 상대 모듈 I/O No
                strReadComand = strReadComand + "00";                                                                // 요구 상대 국번호
                strReadComand = strReadComand +  strHexLenth_HL;                                                     // 요구 데이터 길이 0C18[3098 LEN], 0018[요구 데이터 길이 뒤 부터 24BYTE :09~21 12*2]
                strReadComand = strReadComand + "1000";                                                              // CPU 감시 타이머
                strReadComand = strReadComand + "0114";                                                              // 0401[READ], 1401[WRITE]
                strReadComand = strReadComand + strBitWord;                                                          // 0000[WORD], 0001[BIT]
                strReadComand = strReadComand + strAdddressNo;                                                       // Device Address Hexcode L88 H13 자릿수00
                strReadComand = strReadComand + strAddressDM;                                                        // DEVICE ADDRESS  //Binary Mode D*:A8 M*:90
                strReadComand = strReadComand + strDeviceCount_HL;                                                   // DEVICE 점수 L05 H00 Binary Bit 경우 짝수 word 상관없음
            }
            else
            {
                iLenth = 24 + iCount;
                if ("M" == strAddressDM)
                {
                    strBitWord = "0001";
                }
                else
                {
                    strBitWord = "0000";
                }
                strReadComand = strReadComand + "5000";                                                              // 서브헤더
                strReadComand = strReadComand + "00";                                                                // Network No
                strReadComand = strReadComand + "FF";                                                                // PC No
                strReadComand = strReadComand + "03FF";                                                              // 요구 상대 모듈 I/O No
                strReadComand = strReadComand + "00";                                                                // 요구 상대 국번호
                strReadComand = strReadComand + string.Format("{0:X4}", iLenth);                                     // 요구 데이터 길이 0C18[3098 LEN], 0018[요구 데이터 길이 뒤 부터 24BYTE :09~21 12*2]
                strReadComand = strReadComand + "0010";                                                              // CPU 감시 타이머
                strReadComand = strReadComand + "1401";                                                              // 0401[READ], 1401[WRITE]
                strReadComand = strReadComand + strBitWord;                                                          // 0000[WORD], 0001[BIT]
                strReadComand = strReadComand + strAddressDM + "*";                                                  // DEVICE CODE
                strReadComand = strReadComand + String.Format("{0:D6}", strAdddressNo);                              // DEVICE ADDRESS
                strReadComand = strReadComand + String.Format("{0:D4}", iCount);                                     // DEVICE 점수 

            }
            return strReadComand;
        }
        /******************************************************************************************************************************************************************
        * 제목 : ReadBit
        * 인자 : 
        * 리턴 : 
        * 설명 : 
        ******************************************************************************************************************************************************************/
        public bool ReadBitFromPLC(int iPLC_No, string strAddress, int iCount, ref bool[] bReadData)
        {
            bool bReturn = false;

            do
            {
                try
                {

                    int rsize = 0;
                    byte[] byteBuffer;
                    byte[] data = new byte[2000];

                    String strSendData = ReadCommand(iPLC_No, strAddress, iCount);        
                    byteBuffer = new byte[(strSendData.Length/2)];
                  
                    int[] ihex2dec;
                    string strtemp;
                    int iLenth = 0;
                    ihex2dec = new int[(strSendData.Length / 2)];
                    
                    for (int iLoopCount = 0; iLoopCount < (strSendData.Length / 2); iLoopCount++)
                    {
                        strtemp = strSendData.Substring(iLenth, 2);
                        ihex2dec[iLoopCount] = Convert.ToInt16(strtemp, 16);
                        byteBuffer[iLoopCount] = Convert.ToByte(ihex2dec[iLoopCount]);

                        iLenth = iLenth + 2;

                        if (iLoopCount == (strSendData.Length / 2))
                        {
                            break;
                        }
                    }
                    lock (objPLCLockObject)
                    {
                        PLC_Send(iPLC_No, byteBuffer);

                        iNo = iPLC_No;

                        Thread.Sleep(30);

                        rsize = PLC_Recive(iPLC_No, ref data);
                    }
                    if (rsize > 0)
                    {
                        
                        //인자 data -> rdata로 변경
                        byte[] rdata = new byte[rsize];
                        if (0 == this.objMelescTcp.iProtocoltype)
                        {
                            string strtempCode = null;
                            strtemp = string.Empty;

                            Array.Copy(data, 0, rdata, 0, rsize);

                            for (int iLoopCount = 0; iLoopCount < rdata.Length; iLoopCount++)
                            {
                                strtempCode = strtempCode + string.Format("{0:X2}", rdata[iLoopCount]);
                            }

                            strReciveData = strtempCode;
                        }
                       
                         //ConvertBit(strAddress, iCount, ref bReadData);
                         ConvertWordBit(strAddress, iCount, ref bReadData);
                    }
                }
                catch(System.Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show(ex.Message);
                }
           
                bReturn = true;
               
            } while (false);
     
            return bReturn;
        }
        /******************************************************************************************************************************************************************
       * 제목 : ReadBit
       * 인자 : 
       * 리턴 : 
       * 설명 : 
       ******************************************************************************************************************************************************************/
        public bool ReadWordFromPLC(int iPLC_No, string strAddress, int iCount, ref short[] dReadData)
        {
            // Word 읽기는 FormPLC의 상태/트리거 입력을 가져오는 핵심 경로다.
            bool bReturn = false;

            do
            {
                try
                {

                    int rsize = 0;
                    byte[] byteBuffer;
                    byte[] data = new byte[2000];

                    String strSendData = ReadCommand(iPLC_No, strAddress, iCount);
                    byteBuffer = new byte[(strSendData.Length / 2)];

                    int[] ihex2dec;
                    string strtemp;
                    int iLenth = 0;
                    ihex2dec = new int[(strSendData.Length / 2)];

                    for (int iLoopCount = 0; iLoopCount < (strSendData.Length / 2); iLoopCount++)
                    {
                        strtemp = strSendData.Substring(iLenth, 2);
                        ihex2dec[iLoopCount] = Convert.ToInt16(strtemp, 16);
                        byteBuffer[iLoopCount] = Convert.ToByte(ihex2dec[iLoopCount]);

                        iLenth = iLenth + 2;

                        if (iLoopCount == (strSendData.Length / 2))
                        {
                            break;
                        }
                    }
                    lock (objPLCLockObject)
                    {
                        PLC_Send(iPLC_No, byteBuffer);

                        iNo = iPLC_No;

                        Thread.Sleep(30);

                        rsize = PLC_Recive(iPLC_No, ref data);
                    }
                    if (rsize > 0)
                    {
                       
                        //인자 data -> rdata로 변경
                        byte[] rdata = new byte[rsize];
                        if (0 == this.objMelescTcp.iProtocoltype)
                        {
                            string strtempCode = null;
                            strtemp = string.Empty;

                            Array.Copy(data, 0, rdata, 0, rsize);

                            for (int iLoopCount = 0; iLoopCount < rdata.Length; iLoopCount++)
                            {
                                strtempCode = strtempCode + string.Format("{0:X2}", rdata[iLoopCount]);
                            }

                            strReciveData = strtempCode;
                        }

                        //ConvertBit(strAddress, iCount, ref bReadData);
                        ConvertWord(strAddress, iCount, ref dReadData);
                    }
                }
                catch (System.Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show(ex.Message);
                }

                bReturn = true;

            } while (false);

            return bReturn;
        }
        /******************************************************************************************************************************************************************
       * 제목 : ReadASCIIWord
       * 인자 : 
       * 리턴 : 
       * 설명 : 
       ******************************************************************************************************************************************************************/
        public string ReadWordASCIIFromPLC(int iPLC_No, string strAddress, int iCount)
        {
            string strReturn = null;

            do
            {
                try
                {


                    byte[] byteBuffer;
                    String strSendData = ReadCommand(iPLC_No, strAddress, iCount);
                    byteBuffer = new byte[(strSendData.Length / 2)];

                    int rsize = 0;
                    byte[] data = new byte[2000];

                    int[] ihex2dec;
                    string strtemp;
                    int iLenth = 0;
                    ihex2dec = new int[(strSendData.Length / 2)];
                    if (0 == objMelescTcp.iProtocoltype)
                    {


                        for (int iLoopCount = 0; iLoopCount < (strSendData.Length / 2); iLoopCount++)
                        {
                            strtemp = strSendData.Substring(iLenth, 2);
                            ihex2dec[iLoopCount] = Convert.ToInt16(strtemp, 16);
                            byteBuffer[iLoopCount] = Convert.ToByte(ihex2dec[iLoopCount]);

                            iLenth = iLenth + 2;

                            if (iLoopCount == (strSendData.Length / 2))
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        byteBuffer = new byte[(strSendData.Length)];
                        Array.Copy(Encoding.ASCII.GetBytes(strSendData), 0, byteBuffer, 0, strSendData.Length);
                    }

                    lock (objPLCLockObject)
                    {
                        PLC_Send(iPLC_No, byteBuffer);

                        iNo = iPLC_No;

                        Thread.Sleep(30);

                        rsize = PLC_Recive(iPLC_No, ref data);
                    }

                    if (rsize > 0)
                    {
                        // joon_r - 20140924 : _OnRecieve(this, Encoding.ASCII.GetString(data)); lock 밖에 있던거 안으로 변경
                        //인자 data -> rdata로 변경
                        byte[] rdata = new byte[rsize];
                        if (0 == this.objMelescTcp.iProtocoltype)
                        {
                            string strtempCode = null;
                            strtemp = string.Empty;

                            Array.Copy(data, 0, rdata, 0, rsize);

                            for (int iLoopCount = 0; iLoopCount < rdata.Length; iLoopCount++)
                            {
                                strtempCode = strtempCode + string.Format("{0:X2}", rdata[iLoopCount]);
                            }

                            strReciveData = strtempCode;
                        }

                        //Thread.Sleep(70);
                        ConvertASCII();
                        strReturn = strASCIICode;
                    }
                }
                catch (System.Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show(ex.Message);
                }

               

            } while (false);

            return strReturn;
        }
        /******************************************************************************************************************************************************************
       * 제목 : ReadWord
       * 인자 : 
       * 리턴 : 
       * 설명 : 
       ******************************************************************************************************************************************************************/
        public bool ReadDoubleWordFromPLC(int iPLC_No, string strAddress, int iCount, ref double[] bReadData)
        {
            bool bReturn = false;

            do
            {
                try
                {


                    byte[] byteBuffer;
                    String strSendData = ReadCommand(iPLC_No, strAddress, iCount * 2);
                    byteBuffer = new byte[(strSendData.Length / 2)];

                    int rsize = 0;
                    byte[] data = new byte[2000];

                    int[] ihex2dec;
                    string strtemp;
                    int iLenth = 0;
                    
                    ihex2dec = new int[(strSendData.Length / 2)];
                    
                    if (0 == objMelescTcp.iProtocoltype)
                    {
                            
                            for (int iLoopCount = 0; iLoopCount < (strSendData.Length / 2); iLoopCount++)
                            {
                                strtemp = strSendData.Substring(iLenth, 2);
                                ihex2dec[iLoopCount] = Convert.ToInt16(strtemp, 16);
                                byteBuffer[iLoopCount] = Convert.ToByte(ihex2dec[iLoopCount]);

                                iLenth = iLenth + 2;

                                if (iLoopCount == (strSendData.Length / 2))
                                {
                                    break;
                                }
                            }
                    }
                    else
                    {
                        byteBuffer = new byte[(strSendData.Length)];
                        Array.Copy(Encoding.ASCII.GetBytes(strSendData), 0, byteBuffer, 0, strSendData.Length);
                    }


                    lock (objPLCLockObject)
                    {
                        PLC_Send(iPLC_No, byteBuffer);

                        iNo = iPLC_No;

                        Thread.Sleep(30);

                        rsize = PLC_Recive(iPLC_No, ref data);
                    }
                    if (rsize > 0)
                    {
                        // joon_r - 20140924 : _OnRecieve(this, Encoding.ASCII.GetString(data)); lock 밖에 있던거 안으로 변경
                        //인자 data -> rdata로 변경
                        byte[] rdata = new byte[rsize];
                        if (0 == this.objMelescTcp.iProtocoltype)
                        {
                            string strtempCode = null;
                            strtemp = string.Empty;

                            Array.Copy(data, 0, rdata, 0, rsize);

                            for (int iLoopCount = 0; iLoopCount < rdata.Length; iLoopCount++)
                            {
                                strtempCode = strtempCode + string.Format("{0:X2}", rdata[iLoopCount]);
                            }

                            strReciveData = strtempCode;
                        }
                        if (strReciveData.Length != 38)
                        {
                            ConvertdoubleWord(strAddress, iCount, ref bReadData);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show(ex.Message);
                }

                bReturn = true;

            } while (false);

            return bReturn;
        }
        /******************************************************************************************************************************************************************
         * 제목 : WriteBit
         * 인자 : 
         * 리턴 : 
         * 설명 : 
        ******************************************************************************************************************************************************************/
        public bool WriteBitFromPLC(int iPLC_No, string strAddress, int iCount, bool[] bReadData)
        {
            bool bReturn = false;

            do
            {
                try
                {                 
                    byte[] byteBuffer;

                    String strSendData = WriteCommand(iPLC_No, strAddress, iCount);
                    int[] iRelayOn;
                    iRelayOn = new int[iCount];
                    
                    if (0 == objMelescTcp.iProtocoltype)
                    {
                      
                        for (int iLoopCount = 0; iLoopCount < iCount; iLoopCount++)
                        {
                            string strVal, strValH, strValL;
                           if (bReadData[iLoopCount]) iRelayOn[iLoopCount] = 1;
                           else iRelayOn[iLoopCount] = 0;
                           strVal = string.Format("{0:D2}", iRelayOn[iLoopCount]);
                           strValL = strVal.Substring(1,1);
                           strValH= strVal.Substring(0,1);
                           strVal= strValL + strValH;
                           strSendData = strSendData + strVal;
                        
                        }
                        byteBuffer = new byte[(strSendData.Length / 2)];
                        int[] ihex2dec;
                        string strtemp;
                        int iLenth = 0;
                        ihex2dec = new int[(strSendData.Length / 2)];

                        for (int iLoopCount = 0; iLoopCount < (strSendData.Length / 2); iLoopCount++)
                        {
                            strtemp = strSendData.Substring(iLenth, 2);
                            ihex2dec[iLoopCount] = Convert.ToInt16(strtemp, 16);
                            byteBuffer[iLoopCount] = Convert.ToByte(ihex2dec[iLoopCount]);

                            iLenth = iLenth + 2;

                            if (iLoopCount == (strSendData.Length / 2))
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int iLoopCount = 0; iLoopCount < iCount; iLoopCount++)
                        {
                            if (bReadData[iLoopCount]) iRelayOn[iLoopCount] = 1;
                            else iRelayOn[iLoopCount] = 0;

                            strSendData = strSendData + string.Format("{0:D1}", iRelayOn[iLoopCount]);
                        }
                        byteBuffer = new byte[(strSendData.Length)];
                        Array.Copy(Encoding.ASCII.GetBytes(strSendData), 0, byteBuffer, 0, strSendData.Length);
                    }
                    iNo = iPLC_No;
                    lock (objPLCLockObject)
                    {
                        PLC_Send(iPLC_No, byteBuffer);

                        byte[] data = new byte[2000];
                        int bitSize;
                        bitSize = PLC_Recive(iPLC_No, ref data);
                    }
               
                }
                catch (System.Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show(ex.Message);
                }

                bReturn = true;

            } while (false);

            return bReturn;
        }
        public bool WriteWordBitFromPLC(int iPLC_No, string strAddress, int iCount, bool[] bReadData)
        {
            bool bReturn = false;

            do
            {
                try
                {
                    byte[] byteBuffer;

                    String strSendData = WriteCommand(iPLC_No, strAddress, iCount);
                    int[] iRelayOn;
                    iRelayOn = new int[iCount];

                    if (0 == objMelescTcp.iProtocoltype)
                    {

                        for (int iLoopCount = 0; iLoopCount < iCount; iLoopCount++)
                        {
                            string strVal, strValH, strValL;
                            if (bReadData[iLoopCount]) iRelayOn[iLoopCount] = 1;
                            else iRelayOn[iLoopCount] = 0;
                            strVal = string.Format("{0:X4}", iRelayOn[iLoopCount]);
                            strValL = strVal.Substring(2, 2);
                            strValH = strVal.Substring(0, 2);
                            strVal = strValL + strValH;
                            strSendData = strSendData + strVal;

                        }
                        byteBuffer = new byte[(strSendData.Length / 2)];
                        int[] ihex2dec;
                        string strtemp;
                        int iLenth = 0;
                        ihex2dec = new int[(strSendData.Length / 2)];

                        for (int iLoopCount = 0; iLoopCount < (strSendData.Length / 2); iLoopCount++)
                        {
                            strtemp = strSendData.Substring(iLenth, 2);
                            ihex2dec[iLoopCount] = Convert.ToInt16(strtemp, 16);
                            byteBuffer[iLoopCount] = Convert.ToByte(ihex2dec[iLoopCount]);

                            iLenth = iLenth + 2;

                            if (iLoopCount == (strSendData.Length / 2))
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int iLoopCount = 0; iLoopCount < iCount; iLoopCount++)
                        {
                            if (bReadData[iLoopCount]) iRelayOn[iLoopCount] = 1;
                            else iRelayOn[iLoopCount] = 0;

                            strSendData = strSendData + string.Format("{0:D1}", iRelayOn[iLoopCount]);
                        }
                        byteBuffer = new byte[(strSendData.Length)];
                        Array.Copy(Encoding.ASCII.GetBytes(strSendData), 0, byteBuffer, 0, strSendData.Length);
                    }
                    iNo = iPLC_No;
                    lock (objPLCLockObject)
                    {
                        PLC_Send(iPLC_No, byteBuffer);

                        byte[] data = new byte[2000];
                        int bitSize;
                        bitSize = PLC_Recive(iPLC_No, ref data);
                    }

                }
                catch (System.Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                }

                bReturn = true;

            } while (false);

            return bReturn;
        }
        /******************************************************************************************************************************************************************
      * 제목 : WriteBit
      * 인자 : 
      * 리턴 : 
      * 설명 : 
     ******************************************************************************************************************************************************************/
        public bool WriteWordFromPLC(int iPLC_No, string strAddress, int iCount, short[] bReadData)
        {
            // Word 쓰기는 Vision 상태/결과를 PLC로 반환하는 핵심 경로다.
            bool bReturn = false;

            do
            {
                try
                {
                    byte[] byteBuffer;
                    String strSendData = WriteCommand(iPLC_No, strAddress, iCount);
                    int[] iRelayOn;
                    iRelayOn = new int[iCount];
                    string strVal, strVal_H, strval_L, strVal_H1, strVal_L1;
                    if (0 == objMelescTcp.iProtocoltype)
                    {
                        for (int iLoopCount = 0; iLoopCount < iCount; iLoopCount++)
                        {
                            int dVal = (int)(bReadData[iLoopCount]);
                            strVal = string.Format("{0:X4}", dVal);
                            if(bReadData[iLoopCount] < 0)
                            {
                                strval_L = strVal.Substring( 6, 2 );
                                strVal_H = strVal.Substring( 4, 2 );
                            }
                            else
                            {
                                strval_L = strVal.Substring( 2, 2 );
                                strVal_H = strVal.Substring( 0, 2 );
                            }
                            
                            strSendData = strSendData + strval_L + strVal_H;
                        }
                        byteBuffer = new byte[(strSendData.Length / 2)];

                        int[] ihex2dec;
                        string strtemp;
                        int iLenth = 0;
                        ihex2dec = new int[(strSendData.Length / 2)];

                        for (int iLoopCount = 0; iLoopCount < (strSendData.Length / 2); iLoopCount++)
                        {
                            strtemp = strSendData.Substring(iLenth, 2);
                            ihex2dec[iLoopCount] = Convert.ToInt16(strtemp, 16);
                            byteBuffer[iLoopCount] = Convert.ToByte(ihex2dec[iLoopCount]);

                            iLenth = iLenth + 2;

                            if (iLoopCount == (strSendData.Length / 2))
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                       
                        byteBuffer = new byte[(strSendData.Length)];
                        Array.Copy(Encoding.ASCII.GetBytes(strSendData), 0, byteBuffer, 0, strSendData.Length);
                    }

                    iNo = iPLC_No;
                    lock (objPLCLockObject)
                    {
                        PLC_Send(iPLC_No, byteBuffer);

                        byte[] data = new byte[2000];
                        Thread.Sleep(30);
                        int ibitSize;
                        ibitSize = PLC_Recive(iPLC_No, ref data);
                    }
                    
                }
                catch (System.Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                }

                bReturn = true;

            } while (false);

            return bReturn;
        }

    
        /******************************************************************************************************************************************************************
      * 제목 : ReciveConvertWord
      * 인자 : 
      * 리턴 : 
      * 설명 : 
      ******************************************************************************************************************************************************************/
        private void ConvertdoubleWord(string strAddress, int iCount, ref double[] bReadData)
        {
            string strWord;
            int iDec;
            string  strval_HL, strval_H, strval_L, strval_H1, strval_L1;
          
                strWord = strReciveData.Remove(0, 22);
               
                for (int iLoopCount = 0; iLoopCount < strWord.Length / 8; iLoopCount++)
                {
                    strval_HL = strWord.Substring(iLoopCount * 8, 8);
                    strval_H = strval_HL.Substring(0,2);
                    strval_L = strval_HL.Substring(2,2);
                    strval_H1 = strval_HL.Substring(4, 2);
                    strval_L1 = strval_HL.Substring(6, 2);
                    strval_HL = strval_L1 + strval_H1 + strval_L + strval_H;
                    iDec = int.Parse(strval_HL, System.Globalization.NumberStyles.AllowHexSpecifier);
                   
                    bReadData[iLoopCount] = (Convert.ToDouble(iDec));
                        
                    
                }
           
        }
        /******************************************************************************************************************************************************************
      * 제목 : ReciveConvertBit
      * 인자 : 
      * 리턴 : 
      * 설명 : 
      ******************************************************************************************************************************************************************/
        private void ConvertBit(string strAddress, int iCount, ref bool[] bReadData)
        {
            int iReciveData = strReciveData.Length;
            int istrReciveData = strReciveData.Length;
            string strBit = strReciveData.Remove(0, 22);
            int iLen = strBit.Length;


            for (int iLoopCount = 0; iLoopCount < iCount; iLoopCount++)
            {
                string strResulteData = strBit.Substring(iLoopCount, 1);
                if ("1" == strResulteData) bReadData[iLoopCount] = true;
                else bReadData[iLoopCount] = false;
                strResulteData = "";
            }

            strReciveData = "";

        }
        /******************************************************************************************************************************************************************
    * 제목 : ReciveConvertBit
    * 인자 : 
    * 리턴 : 
    * 설명 : 
    ******************************************************************************************************************************************************************/
        private void ConvertWordBit(string strAddress, int iCount, ref bool[] bReadData)
        {
            // PLC 응답 프레임의 데이터 영역을 bool 배열로 변환한다.
            int iReciveData = strReciveData.Length;
            int istrReciveData = strReciveData.Length;
            string strBit = strReciveData.Remove(0, 22);

            int iLen = strBit.Length;
            for (int iLoopCount = 0; iLoopCount < strBit.Length / 4; iLoopCount++)
            {
                string strval_HL = strBit.Substring(iLoopCount * 4, 4);
                if ("01" == strval_HL.Substring(0, 2)) bReadData[iLoopCount] = true;
                else bReadData[iLoopCount] = false;
            }

            //for (int iLoopCount = 0; iLoopCount < iCount; iLoopCount++)
            //{

            //    string strResulteData = strBit.Substring(iLoopCount, 1);
            //    if ("1" == strResulteData) bReadData[iLoopCount] = true;
            //    else bReadData[iLoopCount] = false;
            //    strResulteData = "";
            //}

            strReciveData = "";

        }
        /******************************************************************************************************************************************************************
* 제목 : ReciveConvertBit
* 인자 : 
* 리턴 : 
* 설명 : 
******************************************************************************************************************************************************************/
        private void ConvertWord(string strAddress, int iCount, ref short[] dReadData)
        {
            // PLC 응답 프레임의 데이터 영역을 short Word 배열로 변환한다.
            int iReciveData = strReciveData.Length;
            int istrReciveData = strReciveData.Length;
            string strBit = strReciveData.Remove(0, 22);
            int iDec;

            for (int iLoopCount = 0; iLoopCount < strBit.Length / 4; iLoopCount++)
            {
                string strval_HL = strBit.Substring(iLoopCount * 4, 4);
                string strval_L = strval_HL.Substring(0,2);
                string strval_H = strval_HL.Substring(2,2);
                strval_HL = strval_H + strval_L;
                iDec = int.Parse(strval_HL, System.Globalization.NumberStyles.AllowHexSpecifier);
                dReadData[iLoopCount] = (short)iDec;
                //if ("01" == strval_HL.Substring(0, 2)) bReadData[iLoopCount] = true;
                //else bReadData[iLoopCount] = false;
            }

          

            strReciveData = "";

        }
        /******************************************************************************************************************************************************************
      * 제목 : ReciveConvertASCII
      * 인자 : 
      * 리턴 : 
      * 설명 : 
      ******************************************************************************************************************************************************************/
        private void ConvertASCII()
        {
            // Word 단위로 받은 ASCII 데이터를 설비 코드 문자열로 복원한다.
            string strWord;
            string strval, strval_HL;
            int iDecValL;
            int iDecValH;
            strASCIICode = "";
            StringBuilder sBuffer = new StringBuilder();
            if (0 == objMelescTcp.iProtocoltype)
            {
                strWord = strReciveData.Remove(0, 22);
                strval = string.Empty;
                for (int iLoopCount = 0; iLoopCount < strWord.Length/4; iLoopCount++)
                {
                    strval_HL = strWord.Substring(iLoopCount * 4, 4);
                    if ("0000" == strval_HL) break;
                    iDecValH = int.Parse(strval_HL.Substring(0, 2),System.Globalization.NumberStyles.AllowHexSpecifier); 
                    iDecValL =  int.Parse(strval_HL.Substring(2, 2),System.Globalization.NumberStyles.AllowHexSpecifier);
                    strval = sBuffer.Append((char)iDecValH).ToString();
                    if (0 == (char)iDecValL) break;
                    strval = sBuffer.Append((char)iDecValL).ToString();
                }
                strASCIICode = strval;
            }
        }
        private void PLC_Send(int iNo, byte[] data)
        {
            // 조립된 Melsec 프레임 바이트를 PLC 소켓으로 전송한다.
            objPLC_Client[iNo].Send(data);
        }
        private int PLC_Recive(int iNo, ref byte[] data)
        {
            // PLC 응답은 lock으로 보호해 Read/Write 스레드 간 수신 충돌을 줄인다.
            int ByteSize = 0;
            lock (LockPLCSendObject)
            {
                ByteSize = objPLC_Client[iNo].Receive(data);
            }
            return ByteSize;
        }
    }
}
