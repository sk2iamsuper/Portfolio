using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using ReelTW;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using Cognex.VisionPro;
using Cognex.VisionPro.Display;
using Cognex.VisionPro.ImageFile;


namespace DKVN
{
    public class ClassFunction
    {
        
        #region Capture and GetImage From Cam
        public Bitmap GetImageFromTrigger(int iCam, int iRetry, int iTimeoutms)
        {
            // 카메라 소프트웨어 트리거를 발생시키고 지정 시간 안에 들어온 최신 이미지를 반환한다.
            Bitmap bmpRet = null;
            DateTime timeStartTrigger = DateTime.Now;
            double timeSpan = 0;

            try
            {
                if(iCam >= 0 && iCam < ClassCommon.MaxDevice)
                {
                    if (ClassSystemConfig.Ins.m_ClsHIK[iCam].IsConnected)
                    {
                        if (iRetry < 1 || iRetry > 3)
                            iRetry = 1;
                        for (int iTry = 0; iTry < iRetry; iTry++)
                        {
                            // 이전 이미지 잔여 버퍼를 비운 뒤 새 트리거 사이클을 시작한다.
                            ClassSystemConfig.Ins.m_ClsHIK[iCam].IsGetImageReady = false;
                            ClassSystemConfig.Ins.m_ClsHIK[iCam].ImageBMPQueue.Clear();

                            // HIK SDK에 소프트웨어 트리거 명령을 전달한다.
                            ClassSystemConfig.Ins.m_ClsHIK[iCam].SetTriggerSoftware(true);

                            // 콜백에서 이미지 수신 플래그가 올라오거나 타임아웃될 때까지 대기한다.
                            while (ClassSystemConfig.Ins.m_ClsHIK[iCam].IsGetImageReady == false)
                            {
                                timeSpan = (DateTime.Now - timeStartTrigger).TotalMilliseconds;
                                if (timeSpan > iTimeoutms || ClassSystemConfig.Ins.m_ClsHIK[iCam].IsGetImageReady)
                                {
                                    break;
                                }
                                Thread.Sleep(10);
                            }

                            // 큐에 들어온 이미지 중 가장 마지막 이미지를 검사 입력으로 사용한다.
                            if (ClassSystemConfig.Ins.m_ClsHIK[iCam].IsGetImageReady)
                            {
                                while (ClassSystemConfig.Ins.m_ClsHIK[iCam].ImageBMPQueue.Count >= 0)
                                {
                                    if (ClassSystemConfig.Ins.m_ClsHIK[iCam].ImageBMPQueue.Count > 0)
                                        bmpRet = ClassSystemConfig.Ins.m_ClsHIK[iCam].ImageBMPQueue.Dequeue();
                                    else
                                        if (ClassSystemConfig.Ins.m_ClsHIK[iCam].ImageBMPQueue.Count == 0)
                                            break;
                                }

                                ClassSystemConfig.Ins.m_ClsHIK[iCam].IsGetImageReady = false;
                                break;
                            }
                        }
                        
                    }
                }

                var time = (DateTime.Now - timeStartTrigger).Milliseconds;
                Console.WriteLine("Time Trigger: " + time.ToString("F3"));
            }
            catch { }
            return bmpRet;
        }

        public bool TryGetImageFromTrigger(int iCam, int iRetry, int iTimeoutms, out Bitmap image)
        {
            // Main에서 예외 처리 없이 이미지 취득 성공 여부만 판단할 수 있게 만든 래퍼다.
            image = GetImageFromTrigger(iCam, iRetry, iTimeoutms);
            return image != null;
        }
        #endregion
        

        #region Save Image
        public void CognexSaveImage(string file_path, ICogImage cogImage, bool is_save_log = true)
        {
            try
            {
                CogImageFileTool vpFileTool = new CogImageFileTool();
                vpFileTool.InputImage = cogImage;
                vpFileTool.Operator.Open(file_path, CogImageFileModeConstants.Write);
                vpFileTool.Run();

                //vpFileTool.Operator.Open(file_path, CogImageFileModeConstants.Closed);

                if (is_save_log)
                SaveLog("Saved Raw Image " + file_path, ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
                
            }
            catch (System.Exception ex)
            {
                if (is_save_log)
                SaveLog("Save Raw Image Fail (" + ex.Message + ")", ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
            }
        }
        public ICogImage CognexOpenImage(string file_path)
        {
            ICogImage cogImage = null;
            try
            {

                CogImageFileTool vpFileTool = new CogImageFileTool();
                vpFileTool.Operator.Open(file_path, CogImageFileModeConstants.Read);
                vpFileTool.Run();

                cogImage = vpFileTool.OutputImage;
            }
            catch (System.Exception ex)
            {
                cogImage = null;
            }
            return cogImage;
        }
        public void CognexDeleteImage(string file_path)
        {
            try
            {
                
            }
            catch (System.Exception ex)
            {
                
            }
        }
        public void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }
        public void SaveImageAsPocket(int IndexPocket, string name, int[] ListPointResult, Bitmap[] ListImgRaw, Bitmap[] ListGraphicImg)
        {
            try
            {
                // 검사 결과별 OK/NG 폴더를 만들고 원본/그래픽 이미지를 설정에 따라 저장한다.
                string _imagePath = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Images\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_MM") + @"\" + DateTime.Now.ToString("yyyy_MM_dd") + @"\";
                string _imagePathGraphic = "";

                if (ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName != null && ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName.TrimEnd() != "")
                {
                    _imagePath += "Model_" + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
                }
                if (IndexPocket >= 0 && ListPointResult != null && ListImgRaw != null && ListGraphicImg != null)
                {
                    for (int iPoint = 0; iPoint < ListPointResult.Length; iPoint++)
                    {
                        string path = _imagePath + "\\POINT" + (iPoint + 1);
                        path += "\\" + (ListPointResult[iPoint] == 1 ? "OK" : "NG");
                        string path_grph = path + "\\Graphic";
                        string name_img = string.Format("{0}_POCKET{1}_P{2}", name, IndexPocket + 1, (iPoint + 1));

                        string strFullPath = "";
                        try
                        {
                            if ((ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local) && ListImgRaw != null && ListImgRaw.Length > iPoint && ListImgRaw[iPoint] != null)
                            {
                                if (!Directory.Exists(path))
                                {
                                    Directory.CreateDirectory(path);
                                }

                                if (ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode == 1)
                                {
                                    strFullPath = path + "\\" + name_img + ".jpg";
                                    ListImgRaw[iPoint].Save(strFullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                }
                                else
                                {
                                    strFullPath = path + "\\" + name_img + ".bmp";
                                    ListImgRaw[iPoint].Save(strFullPath, System.Drawing.Imaging.ImageFormat.Bmp);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                            "EX -> Save Raw Image Error " + ex.Message + " ;" + strFullPath,
                                                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                        }

                        try
                        {
                            if ((ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local) && ListGraphicImg != null && ListGraphicImg.Length > iPoint && ListGraphicImg[iPoint] != null)
                            {
                                if (!Directory.Exists(path_grph))
                                {
                                    Directory.CreateDirectory(path_grph);
                                }
                                ListGraphicImg[iPoint].Save(path_grph + "\\" + name_img + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);

                                SaveLog("Saved Graphic Image", ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
                            }
                        }
                        catch (Exception ex)
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                            "EX -> Save Graphic Image Error " + ex.Message,
                                                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                        }
                    }
                }
            }
            catch
            {

            }
        }
        public void SaveImageRaw(int IndexPos, string strCAM, string name, Bitmap imageRaw)
        {
            try
            {
                string _imagePath = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Images\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_MM") + @"\" + DateTime.Now.ToString("yyyy_MM_dd") + @"\";

                if (ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName != null && ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName.TrimEnd() != "")
                {
                    _imagePath += "Model_" + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
                }
                if (strCAM != null && strCAM.TrimEnd() != "")
                {
                    _imagePath += "\\" + strCAM;
                }

                string strFullPath = "";
                try
                {
                    if ((ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local) && imageRaw != null)
                    {
                        if (!Directory.Exists(_imagePath))
                        {
                            Directory.CreateDirectory(_imagePath); 
                        }

                        if (ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode == 1)
                        {
                            strFullPath = _imagePath + "\\" + name + "_POS_" + IndexPos.ToString("D2") + ".jpg";
                            imageRaw.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                        else
                        {
                            strFullPath = _imagePath + "\\" + name + "_POS_" + IndexPos.ToString("D2") + ".bmp";
                            imageRaw.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Bmp);
                        }

                    }
                }
                catch (Exception ex)
                {
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "EX -> Save Raw Image Error " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }

            }
            catch
            {

            }
        }
        public void SaveImageTrayID(string strOKNG, string name, Bitmap imageRaw1, Bitmap imageRaw2, Bitmap graphicImg)
        {
            try
            {
                string _imagePath = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Images\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_MM") + @"\" + DateTime.Now.ToString("yyyy_MM_dd") + @"\";
                string _imagePathGraphic = "";

                if (ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName != null && ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName.TrimEnd() != "")
                {
                    _imagePath += "Model_" + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
                }
                if (strOKNG != null && strOKNG.TrimEnd() != "")
                {
                    _imagePath += "\\" + strOKNG;
                }
                _imagePathGraphic = _imagePath + "\\Graphic";

                string strFullPath = "";
                try
                {
                    if ((ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local))
                    {
                        if (!Directory.Exists(_imagePath))
                        {
                            Directory.CreateDirectory(_imagePath);
                        }

                        if (imageRaw1 != null)
                        {
                            if (ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode == 1)
                            {
                                strFullPath = _imagePath + "\\" + name + "_CAM1.jpg";
                                imageRaw1.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                            }
                            else
                            {
                                strFullPath = _imagePath + "\\" + name + "_CAM1.bmp";
                                imageRaw1.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Bmp);
                            }
                        }
                        if (imageRaw2 != null)
                        {
                            if (ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode == 1)
                            {
                                strFullPath = _imagePath + "\\" + name + "_CAM2.jpg";
                                imageRaw2.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                            }
                            else
                            {
                                strFullPath = _imagePath + "\\" + name + "_CAM2.bmp";
                                imageRaw2.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Bmp);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "EX -> Save Raw Image Error " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }

                try
                {
                    if ((ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local) && graphicImg != null)
                    {
                        if (!Directory.Exists(_imagePathGraphic))
                        {
                            Directory.CreateDirectory(_imagePathGraphic);
                        }
                        graphicImg.Save(_imagePathGraphic + "\\" + name + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);

                        SaveLog("Saved Graphic Image", ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
                    }
                }
                catch (Exception ex)
                {
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "EX -> Save Graphic Image Error " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }
            }
            catch
            {

            }
        }
        public string  SaveImage(string strOKNG, string name, Bitmap image_origin, Bitmap image_graphic)
        {
            string strPathRet = "";
            string strFullPath = "";
            try
            {
                string _imagePath = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Images\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_MM") + @"\" + DateTime.Now.ToString("yyyy_MM_dd") + @"\";
                string _imagePathGraphic = "";
                strPathRet = DateTime.Now.ToString("yyyy_MM_dd") + @"\";

                if (strOKNG != null && strOKNG.TrimEnd() != "")
                {
                    _imagePath += strOKNG;
                    strPathRet += strOKNG;
                }
                _imagePathGraphic = _imagePath + "\\Graphic";

                
                try
                {
                    if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local && image_origin != null)
                    {
                        if (!Directory.Exists(_imagePath))
                        {
                            Directory.CreateDirectory(_imagePath);
                        }

                        if (ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode == 1)
                        {
                            strFullPath = _imagePath + "\\" + name + ".jpg";
                            strPathRet += "\\" + name + ".jpg";
                            image_origin.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                            SaveLog("Saved Origin Image " + strFullPath, ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
                        }
                        else
                        {
                            strFullPath = _imagePath + "\\" + name + ".bmp";
                            strPathRet += "\\" + name + ".bmp";
                            image_origin.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Bmp);
                            SaveLog("Saved Origin Image " + strFullPath, ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
                        }
                    }

                }
                catch (Exception ex)
                {
                    SaveLog("Save Origin Image Fail " + strFullPath + "(" + ex.Message + ")", ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
                }

                if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local && image_graphic != null)
                {
                    if (!Directory.Exists(_imagePathGraphic))
                    {
                        Directory.CreateDirectory(_imagePathGraphic);
                    }

                    strFullPath = _imagePathGraphic + "\\" + name + ".jpg";
                    image_graphic.Save(strFullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    SaveLog("Saved Graphic Image " + strFullPath, ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local);
                }
            }
            catch
            {

            }
            return strPathRet;
        }

        #endregion

        #region Save Log
        Mutex mutex_log = new Mutex();
        public void SaveLog(string strLog, bool isSaveLog)
        {
            mutex_log.WaitOne();
            if (isSaveLog)
            {
                try
                {
                    string _logPath = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Log\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_MM") + @"\" + DateTime.Now.ToString("yyyy_MM_dd") + @"\";
                    if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local == true)
                    {
                        
                        if (!Directory.Exists(_logPath))
                        {
                            Directory.CreateDirectory(_logPath);
                        }
                        _logPath += @"\PROGRAM.txt";

                        using (StreamWriter objWriter = File.AppendText(_logPath))
                        {
                            objWriter.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + strLog);
                            objWriter.Close();
                        }
                        
                    }
                }
                catch { }
            }

            try
            {
                ClassSystemConfig.Ins.m_UserLog.SetLogOnline(strLog);
            }
            catch { }
            mutex_log.ReleaseMutex();
        }

        public enum SAVING_LOG_TYPE
        {
            PROGRAM,
            DATA,
            PLC,
            HANDLER_CLICKED,
            EXCEPTION,
            DATA_RECEIVE,
            LOADER,
            UNLOADER,
            INSP_CNT
        }
        //public enum SAVING_LOG_TYPE
        //{
        //    PROGRAM,
        //    DATA,
        //    PLC,
        //    HANDLER_CLICKED,
        //    EXCEPTION,
        //    DATA_RECEIVE
        //}
        Mutex mutex = new Mutex();
        public void SaveLog(SAVING_LOG_TYPE log_type, string strLog, bool isSaveLog, bool isUpdateLog)
        {
            if (isSaveLog)
            {
                mutex.WaitOne();
                try
                {
                    string _logPath = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Log\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_MM") + @"\" + DateTime.Now.ToString("yyyy_MM_dd") + @"\";
                    if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local == true)
                    {
                        if (!Directory.Exists(_logPath))
                        {
                            Directory.CreateDirectory(_logPath);
                        }

                        _logPath += log_type.ToString() + ".txt";

                        using (StreamWriter objWriter = File.AppendText(_logPath))
                        {
                            objWriter.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + strLog);
                            objWriter.Close();
                        }

                    }
                }
                catch { }
                mutex.ReleaseMutex();
            }

            if (isUpdateLog)
                ClassSystemConfig.Ins.m_UserLog.SetLogOnline(strLog);
        }

        public void SaveCSVLog(DateTime dateSaving, string runMode, string codeID, int PocketIndex, int final_result, int[] list_result, string[] ListSubHeader, string[] ListSubContent)
        {
            if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local)
            {
                string logPath = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Log\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_MM") + @"\" + DateTime.Now.ToString("yyyy_MM_dd");
                string fileNamePath = "";
                string strLog = "";

                fileNamePath = logPath + "\\" + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName + ".csv";

                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }


                strLog = dateSaving.ToString("MM-dd-yyyy, HH:mm:ss.fff") + ",";
                strLog += runMode + ",";
                strLog += codeID + ",";
                strLog += (final_result == 1 ? "OK" : "NG") + ",";
                strLog += (PocketIndex + 1) + ",";

                if (list_result != null)
                {
                    for (int i = 0; i < list_result.Length; i++)
                    {
                        strLog += list_result[i] + ",";
                    }
                }

                if (ListSubContent != null && ListSubContent.Length > 0)
                {
                    for (int i = 0; i < ListSubContent.Length; i++ )
                        strLog += ListSubContent[i];
                }

                try
                {
                    if (File.Exists(fileNamePath))
                    {
                        FileInfo myFile = new FileInfo(fileNamePath);
                        myFile.IsReadOnly = false;
                        
                    }
                    
                    using (StreamWriter objWriter = new StreamWriter(fileNamePath, true))
                    {
                        if (new FileInfo(fileNamePath).Length == 0)
                        {
                            string strHeader = "DATE, TIME, RUN MODE, CODEID, RESULT, POCKET, ";
                            if (list_result != null)
                            {
                                for (int i = 0; i < list_result.Length; i++)
                                {
                                    strHeader += string.Format(" POINT{0}", i + 1) + ",";
                                }
                            }

                            if (ListSubContent != null && ListSubContent.Length > 0)
                            {
                                for (int i = 0; i < ListSubHeader.Length; i++)
                                    strHeader += ListSubHeader[i];
                            }

                            objWriter.WriteLine(strHeader);
                        }


                        objWriter.WriteLine(strLog);
                        objWriter.Flush();

                        objWriter.Close();
                    }
                }
                catch (Exception ex)
                {
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "EX -> SaveCSVLog Error " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }

            }
        }
        #endregion

        public static Bitmap CapturedScreenShot(Control control)
        {
            // find absolute position of the control in the screen.
            Rectangle rect = control.RectangleToScreen(control.Bounds);

            Bitmap bmp = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);

            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

            return bmp;
        }
        public static Bitmap CapturedScreenShot2(Control control)
        {
            // find absolute position of the control in the screen.
            Control ctrl = control;
            Rectangle rect = new Rectangle(Point.Empty, ctrl.Size);
            do
            {
                rect.Offset(ctrl.Location);
                ctrl = ctrl.Parent;
            }
            while (ctrl != null);

            Bitmap bmp = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);

            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

            return bmp;
        }
    }
}
