using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading;

using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System.Runtime.Serialization.Formatters.Binary;


namespace ReelTW
{
    public partial class FormVisionSetting : Form
    {
        public Main main;
        private List<string> m_ListRecipeName = new List<string>();
        public CogToolBlockEditV2[] m_CogTB = null;

        public FormVisionSetting()
        {
            InitializeComponent();
        }
        public void InitializeUI(Main obj)
        {
            // Vision Form은 Main의 큐/카운터/설정 저장 흐름과 연결되는 검사 엔진 역할을 한다.
            main = obj;
            m_CogTB = new CogToolBlockEditV2[3];
            Initialize_ToolBlock();
            m_iTabSelectedIndex = 0;
            this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;
            if (ClassSystemConfig.Ins.m_ClsCommon.m_dThresholdLimit < 0 || ClassSystemConfig.Ins.m_ClsCommon.m_dThresholdLimit > 1)
            {
                ClassSystemConfig.Ins.m_ClsCommon.m_dThresholdLimit = 0.5;
            }

            UpdateUI();

            ClassSystemConfig.Ins.m_FrmVision.SetModelSpec(); 

        }
        private void FormVisionSetting_Load(object sender, EventArgs e)
        {
            UpdateUI();


            //set spec with formvisionsetting.
            ClassSystemConfig.Ins.m_FrmVision.SetModelSpec();
            
        }
        void Initialize_ToolBlock()
        {
            #region ToolBlock Initialization
            m_CogTB[0] = cogToolBlockEditV21;
            m_CogTB[1] = cogToolBlockEditV22;
            m_CogTB[2] = cogToolBlockEditV23;
            #endregion
        }

        #region Event handler Control
        bool bLastStateNormal = true;
        public void ShowOnScreen()
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = bLastStateNormal ? FormWindowState.Normal : FormWindowState.Maximized;

            this.BringToFront();
        }
        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnMaximum_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                bLastStateNormal = true;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                bLastStateNormal = false;
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Hide();
        }
        private void FormVisionSetting_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }
        #endregion

        #region Enable Drag Winform
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        private void FormMain_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void FormMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void FormMain_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }
        #endregion

        #region Load Recipe
        public List<string> GetListRecipe(string folder)
        {
            // 레시피 폴더명을 기준으로 사용 가능한 모델 목록을 구성한다.
            List<string> listRecipe = new List<string>();
            try
            {
                if (Directory.Exists(folder))
                {
                    string[] temp = Directory.GetDirectories(folder);
                    for (int i = 0; i < temp.Length; i++)
                    {
                        string[] tempSplit = temp[i].Split('\\');
                        listRecipe.Add(tempSplit[tempSplit.Length - 1]); 
                    }
                }
            }
            catch
            {

            }
            return listRecipe;
        }
        public void CheckListRecipes()
        {
            // 실제 폴더와 ListRecipes.xml 정보를 맞춰 레시피 번호/이름 매핑을 유지한다.
            m_ListRecipeName = GetListRecipe(ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath).ToList();
            if (ClassSystemConfig.Ins.m_ClsRecipes == null)
                ClassSystemConfig.Ins.m_ClsRecipes = new ClassRecipes();

            // Check Add Recipe
            if (ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes != null && m_ListRecipeName != null && ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes.Count == 0)
            {
                for (int i = 0; i < m_ListRecipeName.Count; i++)
                {
                    ClassRecipes.RecipeInfo recipe_info = new ClassRecipes.RecipeInfo();
                    recipe_info.RecipeNumber = GenerateNewInteger();
                    recipe_info.RecipeName = m_ListRecipeName[i];
                    string file_name = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath + "\\" + m_ListRecipeName[i];
                    if (File.Exists(file_name))
                    {
                        FileInfo file = new FileInfo(file_name);
                        recipe_info.CreatedDate = file.CreationTime;
                        recipe_info.ModifiedDate = file.CreationTime;
                    }
                    ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes.Add(recipe_info);
                }
            }

            // 
            ClassSystemConfig.Ins.m_ClsRecipes.CurrentRecipeName = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
            for(int i = 0; i < ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes.Count; i++)
            {
                if(ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes[i].RecipeName.Trim() == ClassSystemConfig.Ins.m_ClsRecipes.CurrentRecipeName.Trim())
                {
                    ClassSystemConfig.Ins.m_ClsRecipes.RecipeIndex = i;
                    break;
                }
            }
        }
        public int GenerateNewInteger()
        {
            int iRet = 0;
            if (ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes != null)
            {
                List<int> ListExistNo = new List<int>();
                for(int i = 0; i < ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes.Count ; i++)
                {
                    ListExistNo.Add(ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes[i].RecipeNumber);
                }
                for(int i = 1; i < 1000; i++)
                {
                    if (ListExistNo.Count == 0 || ListExistNo.Contains(i) == false)
                    {
                        iRet = i;
                        break;
                    }
                }
            }
            return iRet;
        }
        public void SaveListRecipes()
        {
            try
            {
                string strPath = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath;
                if (!System.IO.Directory.Exists(strPath))
                    System.IO.Directory.CreateDirectory(strPath);


                ReelTW.ClassCommon.WriteToXmlFile(strPath + "\\ListRecipes.xml", ClassSystemConfig.Ins.m_ClsRecipes, false);
            }
            catch
            {

            }
        }
        public void LoadListRecipes()
        {
            try
            {
                string strConfigFile = string.Format("{0}\\ListRecipes.xml", ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath);
                if (File.Exists(strConfigFile))
                    ClassSystemConfig.Ins.m_ClsRecipes = ClassCommon.ReadFromXmlFile<ClassRecipes>(strConfigFile);
            }
            catch
            {

            }
        }
        private bool LoadModel(CogToolBlockEditV2 cogTB, int iIndexRecipe, string Model_Path, string Model_Name, bool bShowMessage)
        {
            // Cognex ToolBlock(.vpp)을 레시피 번호 기준으로 로딩한다.
            string model_path = Model_Path + "\\";
            model_path += Model_Name + "\\RECIPE" + (iIndexRecipe + 1) + ".vpp";

            try
            {
                if (File.Exists(model_path))
                {
                    if (cogTB.InvokeRequired)
                    {
                        cogTB.Invoke(new MethodInvoker(delegate
                        {
                            cogTB.Subject = (CogToolBlock)CogSerializer.LoadObjectFromFile(model_path, typeof(BinaryFormatter), CogSerializationOptionsConstants.All);
                        }));
                    }
                    else
                    {
                        cogTB.Subject = (CogToolBlock)CogSerializer.LoadObjectFromFile(model_path, typeof(BinaryFormatter), CogSerializationOptionsConstants.All);
                    }
                    
                    ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName = Model_Name;

                    if (bShowMessage)
                        ClassCommon.ShowMessageBoxShort(string.Format("Load Recipe {0} Done", "RECIPE" + (iIndexRecipe + 1) + ".vpp"), Model_Name, 2000);

                    return true;
                }
                else
                {
                    if (bShowMessage)
                        ClassCommon.ShowMessageBoxShort(string.Format("Recipe {0} Not Exist, Please Check Path and Model Name", "RECIPE" + (iIndexRecipe + 1) + ".vpp"), "Warning", 2000);
                    return false;
                }
                main.UpdateCurrentModel();
            }
            catch(Exception ex)
            {
                ClassCommon.ShowMessageBoxShort(string.Format("Load Recipe FAIL + (" + ex.Message + ")", "RECIPE" + (iIndexRecipe + 1) + ".vpp"), Model_Name, 5000);
                main.UpdateCurrentModel();
                return false;
            }
        }
        private void SaveModel(CogToolBlockEditV2 cogTB, int iIndex, string Model_Path, string Model_Name, bool bShowMessage)
        {
            string model_path = Model_Path + "\\";
            model_path += Model_Name + "\\RECIPE" + (iIndex + 1) + ".vpp";

            try
            {
                CogSerializer.SaveObjectToFile(cogTB.Subject, model_path, typeof(BinaryFormatter), CogSerializationOptionsConstants.All);
                if (bShowMessage)
                ClassCommon.ShowMessageBoxShort(string.Format("Save Recipe {0} Done", "RECIPE" + (iIndex + 1) + ".vpp"), Model_Name, 2000);
            }
            catch (Exception ex)
            {
                if (bShowMessage)
                    ClassCommon.ShowMessageBoxShort(string.Format("Save Recipe {0} FAIL, (" + ex.Message + ")", "RECIPE" + (iIndex + 1) + ".vpp"), Model_Name, 2000);
            }
        }
        private void SaveModelCam(CogToolBlockEditV2 acqFifo, string Model_Path, string Model_Name, bool bShowMessage)
        {
            string model_path = Model_Path + "\\";
            model_path += "CAM.vpp";

            try
            {
                CogSerializer.SaveObjectToFile(acqFifo.Subject, model_path, typeof(BinaryFormatter), CogSerializationOptionsConstants.All);
                if (bShowMessage)
                    ClassCommon.ShowMessageBoxShort("Save Recipe CAM Done", Model_Name, 2000);
            }
            catch (Exception ex)
            {
                if (bShowMessage)
                    ClassCommon.ShowMessageBoxShort("Save Recipe CAM FAIL, (" + ex.Message + ")", Model_Name, 2000);
            }
        }
        public bool LoadRecipeStartup(string recipe_name, bool showMessage)
        {
            // 프로그램 시작 시 저장된 레시피를 로딩하고 ModelConfig까지 함께 반영한다.
            bool bRet = true;
            try
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                    "Start Load Model " + recipe_name,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);


                for (int i = 0; i < m_CogTB.Length; i++ )
                    bRet &= LoadModel(m_CogTB[i], i, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath, recipe_name, false);

                try
                {
                    if (true)
                    {
                        ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName = recipe_name;
                        string strConfigFile = string.Format("{0}\\{1}\\ModelConfig.xml", ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName);
                        if (File.Exists(strConfigFile))
                            ClassSystemConfig.Ins.m_ClsModelConfig = ClassCommon.ReadFromXmlFile<ClassModelConfig>(strConfigFile);
                        ClassSystemConfig.Ins.m_FrmSpec.CheckCondition();
                    }

                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                    "Load Model Completed -> " + recipe_name,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }
                catch
                {

                }
                if (showMessage)
                    ClassCommon.ShowMessageBoxShort(bRet ? "Load Completed" : "Load Fail", "Load Model", 2000);
                txbRecipeName.Text = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
                
            }
            catch
            {
                bRet = false;
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                    "Load Model Failed -> " + recipe_name,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
            return bRet;
        }
        public bool LoadRecipeAuto(int model_Value, bool showMessage)
        {
            // PLC에서 모델 번호가 들어오는 경우 번호를 레시피명으로 변환해 자동 로딩한다.
            bool bLoad = false;
            try
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                    "Start Load Model From PLC -> " + model_Value,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);

                if(ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes != null && ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes.Count > 0)
                {
                    bool bExistRecipe = false;
                    string strRecipe = "";
                    for(int i = 0; i < ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes.Count; i++)
                    {
                        if(ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes[i].RecipeNumber == model_Value)
                        {
                            bExistRecipe = true;
                            strRecipe = ClassSystemConfig.Ins.m_ClsRecipes.ListRecipes[i].RecipeName;
                            break;
                        }
                    }

                    if (bExistRecipe)
                    {
                        bLoad = true;
                        ClassSystemConfig.Ins.m_ClsCommon.StartLoadingForm();
                        DateTime timeBeginLoad = DateTime.Now;

                        for (int iIndex = 0; iIndex < m_CogTB.Length; iIndex++)
                        {
                            if (LoadModel(m_CogTB[iIndex], iIndex, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath, strRecipe, true) == false)
                                bLoad &= false;
                        }

                        var time_Loading = (DateTime.Now - timeBeginLoad).TotalMilliseconds;
                        if (time_Loading < 100)
                            Thread.Sleep(100);

                        ClassSystemConfig.Ins.m_ClsCommon.StopLoadingForm();

                        if (bLoad)
                        {
                            ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName = strRecipe;
                            string strConfigFile = string.Format("{0}\\{1}\\ModelConfig.xml", ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName);
                            if (File.Exists(strConfigFile))
                            {
                                ClassSystemConfig.Ins.m_ClsModelConfig = ClassCommon.ReadFromXmlFile<ClassModelConfig>(strConfigFile);
                            }
                            ClassSystemConfig.Ins.m_FrmSpec.CheckCondition();

                            main.SaveDeviceConfig(false);
                            ClassSystemConfig.Ins.m_UserLog.SetLogOnline("Load Recipe \'" + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName + "\' Done");

                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                        "Load Model From PLC Completed -> " + model_Value,
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, false);
                        }

                        if (showMessage)
                            ClassCommon.ShowMessageBoxShort(bLoad ? "Load Completed" : "Load Fail", "Load Model", 2000);

                        main.UpdateCurrentModel();
                        ClassSystemConfig.Ins.m_FrmVision.SetModelSpec();

                    }
                    else
                    {
                        if (showMessage)
                            ClassCommon.ShowMessageBoxShort("Recipe is not exist!\nModel Number = " + model_Value, "Load Model", 2000);
                    }
                }
                else
                {
                    if (showMessage)
                        ClassCommon.ShowMessageBoxShort("List Recipe is Empty, Please check again.\nModel Number = " + model_Value, "Load Model", 2000);
                }


            }

            catch
            {

            }

            return false;
        }

         private void btnLoadRecipe_Click(object sender, EventArgs e)
         {
             if (ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName != "")
             {
                 string strRecipe = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
                 if (MessageBox.Show("Do you want to load this model?", strRecipe, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                 {
                     bool bLoad = true;

                     ClassSystemConfig.Ins.m_ClsCommon.StartLoadingForm();
                     DateTime timeBeginLoad = DateTime.Now;
                     for (int iIndex = 0; iIndex < m_CogTB.Length; iIndex++)
                     {
                         if (LoadModel(m_CogTB[iIndex], iIndex, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath, strRecipe, true) == false)
                             bLoad &= false;
                     }

                     var time_Loading = (DateTime.Now - timeBeginLoad).TotalMilliseconds;
                     if (time_Loading < 100)
                         Thread.Sleep(100);

                     ClassSystemConfig.Ins.m_ClsCommon.StopLoadingForm();

                     if (bLoad)
                     {
                         ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName = strRecipe;
                         string strConfigFile = string.Format("{0}\\{1}\\ModelConfig.xml", ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName);
                         if (File.Exists(strConfigFile))
                         {
                             ClassSystemConfig.Ins.m_ClsModelConfig = ClassCommon.ReadFromXmlFile<ClassModelConfig>(strConfigFile);
                         }
                         ClassSystemConfig.Ins.m_FrmSpec.CheckCondition();
                         
                         main.SaveDeviceConfig(false);
                         ClassSystemConfig.Ins.m_UserLog. SetLogOnline("Load Recipe \'" + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName + "\' Done");
                     }

                 }
             }
             main.UpdateCurrentModel();
             ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Load Model Handle",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
             ClassSystemConfig.Ins.m_FrmVision.SetModelSpec();
         }

         private void btnSaveRecipe_Click(object sender, EventArgs e)
         {
             if (ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName != null)
             {
                 string strRecipe = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
                 if (MessageBox.Show("Do you want to Save this model?", strRecipe, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                 {
                     
                     for (int iIndex = 0; iIndex < m_CogTB.Length; iIndex++)
                     SaveModel(m_CogTB[iIndex], 
                                iIndex,
                                ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath,
                                strRecipe,
                                true
                                );
                     ClassCommon.ShowMessageBox("Save Done", "Save Recipe : " + strRecipe);
                 }

                 try
                 {
                     string strPath = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath + "\\" + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
                     if (!System.IO.Directory.Exists(strPath))
                         System.IO.Directory.CreateDirectory(strPath);

                     if (!File.Exists(strPath + "\\ModelConfig.xml"))
                     ReelTW.ClassCommon.WriteToXmlFile(strPath + "\\ModelConfig.xml", ClassSystemConfig.Ins.m_ClsModelConfig, false);
                 }
                 catch
                 {

                 }
             }
             ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Save Model",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
         }

   
         public bool DeleteRecipeFolder(string strFolder)
         {
             bool bDelete = false;
             try
             {
                 if (Directory.Exists(strFolder))
                 {
                     Directory.Delete(strFolder, true);

                 }
                 bDelete = true;
             }
             catch
             {
                 bDelete = false;
             }
             return bDelete;
         }
         public bool CopyRecipeFolder(string sourcePath, string targetPath)
         {
             try
             {
                 Directory.CreateDirectory(targetPath);

                 //Now Create all of the directories
                 foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                 {
                     Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
                 }

                 //Copy all the files & Replaces any files with the same name
                 foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                 {
                     File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
                 }
                 return true;
             }
             catch { return false; }
         }
        private void CheckCopyNewRecipe(string recipe_name)
         {
             string strSrcFile = string.Format("{0}\\{1}\\RECIPE2.vpp",
                                 ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath,
                                 recipe_name);
             string strTargetFile = string.Format("{0}\\{1}\\RECIPE3.vpp",
                                  ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath,
                                  recipe_name);

            if(File.Exists(strSrcFile) && !File.Exists(strTargetFile))
            {
                try
                {
                    File.Copy(strSrcFile, strTargetFile);
                }
                catch
                {

                }
            }
         }
        #endregion

        #region Load Image From CAM and File
         private void btnFromCam_Click(object sender, EventArgs e)
         {
             
             try
             {
                 int iIndexSelected = cbBoxInspectType.SelectedIndex;
                 if (iIndexSelected < 0)
                     return;

                 panelOpenFolder.Visible = false;
                 int iSelectedType = cbBoxInspectType.SelectedIndex;
                 if (iSelectedType >= 0 && iSelectedType < cbBoxInspectType.Items.Count)
                 {
                     
                 }
                
             }
             catch
             {

             }
         }

         private void btnFromFile_Click(object sender, EventArgs e)
         {
             try
             {
                 int iIndexSelected = cbBoxInspectType.SelectedIndex;
                 if (iIndexSelected < 0)
                     return;

                 panelOpenFolder.Visible = false;
                 int iSelectedType = cbBoxInspectType.SelectedIndex;
                 if (iSelectedType >= 0 && iSelectedType < cbBoxInspectType.Items.Count)
                 {
                     OpenFileDialog fileDialog = new OpenFileDialog();
                     if (Directory.Exists(ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath))
                     {
                         //fileDialog.InitialDirectory = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath;
                     }
                     //fileDialog.Filter = "Images |*.cdb;";
                     if (fileDialog.ShowDialog() == DialogResult.OK)
                     {
                         Bitmap bmp = new Bitmap(fileDialog.FileName);

                         CogImage8Grey imageInput = new CogImage8Grey(bmp);
                         RunProcess(iIndexSelected, imageInput);
                     }

                 }
             }
             catch
             {

             }
         }
        #endregion

        #region Open Image Folder
         string m_strStrFolderSelect = "";
         int m_IndexRun = -1;
         List<string> m_ListImageFiles = new List<string>();
         public static String[] GetFilesFrom(String searchFolder, bool isRecursive)
         {
             List<String> filesFound = new List<String>();
             String[] filters = new String[] { "cdb"};
             var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
             foreach (var filter in filters)
             {
                 filesFound.AddRange(Directory.GetFiles(searchFolder, String.Format("*.{0}", filter), searchOption));
             }
             return filesFound.ToArray();
         }
         private void btnOpenFolder_Click(object sender, EventArgs e)
         {
             panelOpenFolder.Visible = true;
             m_ListImageFiles.Clear();

             FolderBrowserDialog fldDialog = new FolderBrowserDialog();
             if (m_strStrFolderSelect == "")
                 m_strStrFolderSelect = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath;

             if (Directory.Exists(m_strStrFolderSelect))
             {
                 fldDialog.SelectedPath = m_strStrFolderSelect;
             }
             if (fldDialog.ShowDialog() == DialogResult.OK)
             {
                 m_strStrFolderSelect = fldDialog.SelectedPath;
                 String[] listImages = GetFilesFrom(m_strStrFolderSelect, false);
                 if (listImages != null)
                 {
                     m_IndexRun = 0;
                     lbTotalImages.Text = "Total : " + listImages.Length;
                     lbIndexImage.Text = "Index : " + m_IndexRun;
                     m_ListImageFiles.AddRange(listImages);
                 }
             }
         }

         
         private void btnRunNext_Click(object sender, EventArgs e)
         {
             int iIndexSelected = cbBoxInspectType.SelectedIndex;
             if (iIndexSelected < 0)
                 return;

             if(m_ListImageFiles != null && m_ListImageFiles.Count > 0)
             {
                 m_IndexRun++;
                 if(m_IndexRun > 0 && m_IndexRun <= m_ListImageFiles.Count)
                 {
                     ICogImage imageInput = ClassSystemConfig.Ins.m_ClsFunc.CognexOpenImage(m_ListImageFiles[m_IndexRun - 1]);

                     RunProcess(iIndexSelected, imageInput);
                     lbIndexImage.Text = "Index : " + (m_IndexRun - 1);
                     
                 }
                 else
                 {
                     m_IndexRun = 0;
                 }
             }
         }

         private void btnRunBack_Click(object sender, EventArgs e)
         {
             int iIndexSelected = cbBoxInspectType.SelectedIndex;
             if (iIndexSelected < 0)
                 return;

             if (m_ListImageFiles != null && m_ListImageFiles.Count > 0)
             {
                 m_IndexRun--;
                 if (m_IndexRun > 0 && m_IndexRun <= m_ListImageFiles.Count)
                 {
                     ICogImage imageInput = ClassSystemConfig.Ins.m_ClsFunc.CognexOpenImage(m_ListImageFiles[m_IndexRun - 1]);

                     RunProcess(iIndexSelected, imageInput);

                     lbIndexImage.Text = "Index : " + (m_IndexRun - 1);
                     
                 }
                 else
                 {
                     m_IndexRun = m_ListImageFiles.Count + 1;
                 }
             }
         }

         System.Threading.Thread m_ThreadLoop = null;
         int iDelayTime = 500;
         bool bLoop = false;
         private void cbRunLoop_CheckedChanged(object sender, EventArgs e)
         {
             bLoop = cbRunLoop.Checked;
             numericTimeRunLoop.Enabled = !bLoop;
             if(cbRunLoop.Checked)
             {
                 m_ThreadLoop = new System.Threading.Thread(ThreadRunLoop);
                 m_ThreadLoop.Start();
             }
             else
             {
                 if(m_ThreadLoop != null)
                 {
                     //m_ThreadLoop.Abort();
                 }
             }
         }
         
        private void ThreadRunLoop()
         {
             int iIndexSelected = -1;
            if(cbBoxInspectType.InvokeRequired)
            {
                cbBoxInspectType.Invoke(new MethodInvoker(delegate
                {
                    iIndexSelected = cbBoxInspectType.SelectedIndex;
                }));
            }
            else
            {
                iIndexSelected = cbBoxInspectType.SelectedIndex;
            }

            if (m_IndexRun < 0 || m_IndexRun >= m_ListImageFiles.Count)
            {
                m_IndexRun = 0;
            }

             while (bLoop)
            {
                try
                {
                    if (m_ListImageFiles != null && m_ListImageFiles.Count > 0)
                    {
                        if (m_IndexRun >= 0 && m_IndexRun < m_ListImageFiles.Count)
                        {
                            if (true)
                            {
                                ICogImage imageInput = ClassSystemConfig.Ins.m_ClsFunc.CognexOpenImage(m_ListImageFiles[m_IndexRun]);
                                RunProcess(iIndexSelected, imageInput);
                            }
                            ClassCommon.ControlTextInvoke(lbIndexImage,"Index : " + m_IndexRun);
                            m_IndexRun++;
                        }
                        else
                        {
                            ClassCommon.CheckBoxInvoke(cbRunLoop, false);
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch { }
                System.Threading.Thread.Sleep(iDelayTime);
            }
         }

        private void numericTimeRunLoop_ValueChanged(object sender, EventArgs e)
        {
            iDelayTime = (int)numericTimeRunLoop.Value;
        }
        #endregion

        #region Processing Run
        Stopwatch m_stopWatch = new Stopwatch();
        Mutex m_mutex = new Mutex();

        public void RunProcessCodeID(Bitmap bmp, int index_recipe, int index_control, bool IsCheckQR, bool IsCheckAlign, bool IsTrayID, ref string CodeID, ref int iResult)
        {
            // Main 수동/자동 검사에서 공통으로 사용하는 CodeID 검사 진입점이다.
            try
            {
                CogImage8Grey image1 = null;
                if (bmp != null)
                    image1 = new CogImage8Grey(bmp);

                ClsSaveImage clsData = new ClsSaveImage();
                Stopwatch _stopWatch = new Stopwatch();
                _stopWatch.Start();

                    if (m_CogTB[index_recipe] != null && m_CogTB[index_recipe].Subject != null)
                    {
                        // ToolBlock 입력값을 세팅한 뒤 Cognex 검사 알고리즘을 실행한다.
                        if (m_CogTB[index_recipe].Subject.Inputs.Contains("InputImage") && image1 != null)
                        m_CogTB[index_recipe].Subject.Inputs["InputImage"].Value = image1;
                    else
                        if (m_CogTB[index_recipe].Subject.Inputs.Contains("InputImage1") && image1 != null)
                            m_CogTB[index_recipe].Subject.Inputs["InputImage1"].Value = image1;

                    if (m_CogTB[index_recipe].Subject.Inputs.Contains("IsTrayID"))
                        m_CogTB[index_recipe].Subject.Inputs["IsTrayID"].Value = IsTrayID;
                    if (m_CogTB[index_recipe].Subject.Inputs.Contains("IsCheckQR"))
                        m_CogTB[index_recipe].Subject.Inputs["IsCheckQR"].Value = IsCheckQR;
                    if (m_CogTB[index_recipe].Subject.Inputs.Contains("IsCheckAlign"))
                        m_CogTB[index_recipe].Subject.Inputs["IsCheckAlign"].Value = IsCheckAlign;


                    m_CogTB[index_recipe].Subject.Run();

                    if (m_CogTB[index_recipe].Subject.Outputs.Contains("CodeID") && m_CogTB[index_recipe].Subject.Outputs["CodeID"].Value != null)
                    {
                        // ToolBlock 출력값을 Main이 사용할 CodeID/OKNG 결과로 변환한다.
                        string strCodeID = m_CogTB[index_recipe].Subject.Outputs["CodeID"].Value.ToString();
                        if (strCodeID != null)
                            CodeID = strCodeID.Trim();
                    }
                    if (m_CogTB[index_recipe].Subject.Outputs.Contains("iResult") && m_CogTB[index_recipe].Subject.Outputs["iResult"].Value != null)
                    {
                        iResult = (int)m_CogTB[index_recipe].Subject.Outputs["iResult"].Value;
                    }

                    _stopWatch.Stop();

                    // Set Record Display
                    if (m_CogTB[index_recipe].Subject.CreateLastRunRecord().SubRecords.ContainsKey("CopyImage.OutputImage"))
                    {
                        ClassSystemConfig.Ins.m_UserDisplay[index_control].cogRecordDisplay.Record = m_CogTB[index_recipe].Subject.CreateLastRunRecord().SubRecords["CopyImage.OutputImage"];
                    }
                    ClassSystemConfig.Ins.m_UserDisplay[index_control].UpdateDisplayUI(_stopWatch.Elapsed.TotalMilliseconds);

                    clsData.CamIndex = index_control;
                    if(ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local)
                    clsData.OriginImage = bmp;
                    if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local)
                    clsData.GraphicImage = ClassSystemConfig.Ins.m_UserDisplay[index_control].GetImageGraphic();
                    clsData.Name = string.Format("{0}_{1}_{2}{3}",
                        index_control == 0 ? "LOADER" : "ULD",
                        DateTime.Now.ToString("HHmmssfff"),
                        CodeID,
                        IsCheckQR ? "_JIG" : "");
                    clsData.SavingTime = DateTime.Now;
                    clsData.StrOKNG = (iResult == 1) ? "OK" : "NG";
                    clsData.RunMode = main.m_eRunningMode.ToString();
                    main.m_QueSaveImage.Enqueue(clsData);
                }
            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "Vision.RunProcessCodeID -> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
            GC.Collect();
        }

        public void RunProcess(int indexTool, ICogImage inputImage)
        {
            // Vision 설정 화면에서 단일 ToolBlock을 시험 실행할 때 사용하는 메서드다.
            m_mutex.WaitOne();
            try
            {
                #region Check Condition
                if (indexTool < 0 || indexTool > m_CogTB.Length) //ClsPocketPosition.MAXPOINT
                    return;

                #endregion

                {
                    if (m_CogTB[indexTool] != null && m_CogTB[indexTool].Subject != null)
                    {
                        #region Init and Run ToolBlock
                        Stopwatch _stopWatch = new Stopwatch();
                        _stopWatch.Start();

                        if (m_CogTB[indexTool].Subject.Inputs.Contains("InputImage"))
                            m_CogTB[indexTool].Subject.Inputs["InputImage"].Value = inputImage;

                        if (m_CogTB[indexTool].Subject.Inputs.Contains("Show"))
                            m_CogTB[indexTool].Subject.Inputs["Show"].Value = ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic;

                        // Run ToolBlock
                        try
                        {
                            m_CogTB[indexTool].Subject.Run();
                        }
                        catch { throw; }
                        _stopWatch.Stop();

                        // Get Output
                        ICogImage outImage = null;

                        if (m_CogTB[indexTool].Subject.Outputs.Contains("OutputImage") && m_CogTB[indexTool].Subject.Outputs["OutputImage"].Value != null)
                        {
                            outImage = m_CogTB[indexTool].Subject.Outputs["OutputImage"].Value as ICogImage;
                        }


                        #endregion

                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "Vision.RunProcess -> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
            m_mutex.ReleaseMutex();
        }

        public void RunProcessCAM1(ICogImage inputImage, int index_control, int index_recipe,  ref int iResult, ref string qr_code, ref double angle)
        {
            // CAM1 검사는 결과, 각도, QR 데이터를 산출해 PLC 반환 데이터의 원천이 된다.
            try
            {
                #region Check Condition
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                        "INSP_CAM Index = " + index_control + " -> Begin RunProcess",
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                #endregion

                {
                    if (m_CogTB[index_recipe] != null && m_CogTB[index_recipe].Subject != null)
                    {
                        #region Init and Run ToolBlock
                        ClsSaveImage clsData = new ClsSaveImage();
                        Stopwatch _stopWatch = new Stopwatch();
                        _stopWatch.Start();

                        if (m_CogTB[index_recipe].Subject.Inputs.Contains("InputImage"))
                            m_CogTB[index_recipe].Subject.Inputs["InputImage"].Value = inputImage;

                        // ToolBlock 실행 전 화면 표시 옵션을 입력으로 전달한다.
                        if (m_CogTB[index_recipe].Subject.Inputs.Contains("Show"))
                            m_CogTB[index_recipe].Subject.Inputs["Show"].Value = ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic;

                        // Run ToolBlock
                        try
                        {
                            m_CogTB[index_recipe].Subject.Run();
                        }
                        catch { throw; }
                        

                        // Get Output
                        string Header = "";
                        string HeaderLog = "";
                        bool bResult = false;

                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("Header") && m_CogTB[index_recipe].Subject.Outputs["Header"].Value != null)
                        {
                            Header = (string)m_CogTB[index_recipe].Subject.Outputs["Header"].Value;
                        }
                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("HeaderLog") && m_CogTB[index_recipe].Subject.Outputs["HeaderLog"].Value != null)
                        {
                            HeaderLog = (string)m_CogTB[index_recipe].Subject.Outputs["HeaderLog"].Value;
                        }

                        // get Result
                        // bResult/AlignValue/CodeString 출력값을 설비 결과 데이터로 변환한다.
                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("bResult") && m_CogTB[index_recipe].Subject.Outputs["bResult"].Value != null)
                        {
                            bResult = (bool)m_CogTB[index_recipe].Subject.Outputs["bResult"].Value;
                        }
                        if (bResult == true)
                            iResult = 1;
                        else
                            iResult = 2; // or 0

                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("AlignValue") && m_CogTB[index_recipe].Subject.Outputs["AlignValue"].Value != null)
                        {
                            angle = (double)m_CogTB[index_recipe].Subject.Outputs["AlignValue"].Value;
                        }
                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("CodeString") && m_CogTB[index_recipe].Subject.Outputs["CodeString"].Value != null)
                        {
                            qr_code = (string)m_CogTB[index_recipe].Subject.Outputs["CodeString"].Value;
                        }
                        var time = _stopWatch.Elapsed.TotalMilliseconds;
                        _stopWatch.Stop();
                        #endregion

                        #region Display and Save to Buffer
                        // Set Record Display
                        if (m_CogTB[index_recipe].Subject.CreateLastRunRecord().SubRecords.ContainsKey("CopyTool.OutputImage"))
                        {
                            ClassSystemConfig.Ins.m_UserDisplay[index_control].cogRecordDisplay.Record = m_CogTB[index_recipe].Subject.CreateLastRunRecord().SubRecords["CopyTool.OutputImage"];
                        }
                        else
                            ClassSystemConfig.Ins.m_UserDisplay[index_control].SetImage(inputImage);


                        ClassSystemConfig.Ins.m_UserDisplay[index_control].UpdateDisplayUI(time);

                        #endregion

                        #region Update Counter
                        //if (iResult == 1)
                        //    ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[0]++;
                        //else
                        //    ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[1]++;
                        main.UpdateResultCount(false);
                        #endregion

                        #region Add To Saving Buffer
                        clsData.CamIndex = index_control;
                        if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local && inputImage != null)
                            clsData.OriginImage = inputImage.ToBitmap();
                        if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local)
                            clsData.GraphicImage = ClassSystemConfig.Ins.m_UserDisplay[index_control].GetImageGraphic();

                        clsData.Name = string.Format("CAM1_{0}_{1}_{2}",
                            DateTime.Now.ToString("HHmmssfff"),
                            qr_code,
                            bResult ? "OK" : "NG");
                        clsData.SavingTime = DateTime.Now;
                        clsData.StrOKNG = (iResult == 1) ? "OK" : "NG";
                        clsData.RunMode = main.m_eRunningMode.ToString();
                        main.m_QueSaveImage.Enqueue(clsData);
                        #endregion

                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "Vision.RunProcess -> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
        }
        public void RunProcessCAM2(ICogImage inputImage, int index_control, int index_recipe, ref int iResult, ref string qr_code)
        {
            // CAM2 검사는 단순 OK/NG와 코드 문자열 중심으로 결과를 만든다.
            try
            {
                #region Check Condition
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                        "INSP_CAM Index = " + index_control + " -> Begin RunProcess",
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                #endregion

                {
                    if (m_CogTB[index_recipe] != null && m_CogTB[index_recipe].Subject != null)
                    {
                        #region Init and Run ToolBlock
                        ClsSaveImage clsData = new ClsSaveImage();
                        Stopwatch _stopWatch = new Stopwatch();
                        _stopWatch.Start();

                        if (m_CogTB[index_recipe].Subject.Inputs.Contains("InputImage"))
                            m_CogTB[index_recipe].Subject.Inputs["InputImage"].Value = inputImage;

                        // ToolBlock 실행 결과는 화면 표시, 카운터, 이미지 저장 큐로 이어진다.
                        if (m_CogTB[index_recipe].Subject.Inputs.Contains("Show"))
                            m_CogTB[index_recipe].Subject.Inputs["Show"].Value = ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic;

                        // Run ToolBlock
                        try
                        {
                            m_CogTB[index_recipe].Subject.Run();
                        }
                        catch { throw; }


                        // Get Output
                        string Header = "";
                        string HeaderLog = "";
                        bool bResult = false;

                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("Header") && m_CogTB[index_recipe].Subject.Outputs["Header"].Value != null)
                        {
                            Header = (string)m_CogTB[index_recipe].Subject.Outputs["Header"].Value;
                        }
                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("HeaderLog") && m_CogTB[index_recipe].Subject.Outputs["HeaderLog"].Value != null)
                        {
                            HeaderLog = (string)m_CogTB[index_recipe].Subject.Outputs["HeaderLog"].Value;
                        }

                        // get Result
                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("bResult") && m_CogTB[index_recipe].Subject.Outputs["bResult"].Value != null)
                        {
                            bResult = (bool)m_CogTB[index_recipe].Subject.Outputs["bResult"].Value;
                        }
                        if (bResult == true)
                            iResult = 1;
                        else
                            iResult = 2; // or 0

                        if (m_CogTB[index_recipe].Subject.Outputs.Contains("CodeString") && m_CogTB[index_recipe].Subject.Outputs["CodeString"].Value != null)
                        {
                            qr_code = (string)m_CogTB[index_recipe].Subject.Outputs["CodeString"].Value;
                        }
                        var time = _stopWatch.Elapsed.TotalMilliseconds;
                        _stopWatch.Stop();
                        #endregion

                        #region Display and Save to Buffer
                        // Set Record Display
                        if (m_CogTB[index_recipe].Subject.CreateLastRunRecord().SubRecords.ContainsKey("CopyTool.OutputImage"))
                        {
                            ClassSystemConfig.Ins.m_UserDisplay[index_control].cogRecordDisplay.Record = m_CogTB[index_recipe].Subject.CreateLastRunRecord().SubRecords["CopyTool.OutputImage"];
                        }
                        else
                            ClassSystemConfig.Ins.m_UserDisplay[index_control].SetImage(inputImage);


                        ClassSystemConfig.Ins.m_UserDisplay[index_control].UpdateDisplayUI(time);

                        #endregion

                        #region Update Counter
                        if (iResult == 1)
                            ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[0]++;
                        else
                            ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[1]++;
                        main.UpdateResultCount(false);
                        #endregion

                        #region Add To Saving Buffer
                        clsData.CamIndex = index_control;
                        if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local && inputImage != null)
                            clsData.OriginImage = inputImage.ToBitmap();
                        if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local)
                            clsData.GraphicImage = ClassSystemConfig.Ins.m_UserDisplay[index_control].GetImageGraphic();

                        clsData.Name = string.Format("CAM2_{0}_{1}_{2}",
                            DateTime.Now.ToString("HHmmssfff"),
                            qr_code,
                            bResult ? "OK" : "NG");
                        clsData.SavingTime = DateTime.Now;
                        clsData.StrOKNG = (iResult == 1) ? "OK" : "NG";
                        clsData.RunMode = main.m_eRunningMode.ToString();
                        main.m_QueSaveImage.Enqueue(clsData);
                        #endregion

                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "Vision.RunProcess -> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
        }
        #endregion

        #region Edit UI
        public void UpdateUI()
        {
            // Add Inspect Type List
            cbBoxInspectType.Items.Clear();
            cbBoxInspectType.Items.Add("LOADER");
            cbBoxInspectType.Items.Add("INSPECTION");
            //cbBoxInspectType.Items.Add("INSPECT CNT");

            if (cbBoxInspectType.Items.Count > 0)
                cbBoxInspectType.SelectedIndex = 0;

            // Add Recipes List
            m_ListRecipeName.Clear();
            m_ListRecipeName = GetListRecipe(ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath).ToList();
            txbRecipeName.Text = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
        }
        private void cbBoxInspectType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int iSelectedModel = cbBoxInspectType.SelectedIndex;
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            m_ListRecipeName.Clear();
            m_ListRecipeName = GetListRecipe(ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath).ToList();
            txbRecipeName.Text = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
        }

        #endregion
        
        private void btnSettingModelSpec_Click(object sender, EventArgs e)
        {
            if (ClassSystemConfig.Ins.m_FrmSpec == null)
                ClassSystemConfig.Ins.m_FrmSpec = new FormModelSpec();

            ClassSystemConfig.Ins.m_ClsModelConfig.ModelName = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName;
            ClassSystemConfig.Ins.m_FrmSpec.InitializeUI();
            ClassSystemConfig.Ins.m_FrmSpec.ShowDialog();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Show Spec Setting",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        public void SetModelSpec()
        {
            try
            {
                if (m_CogTB != null)
                    for (int iTB = 1; iTB < m_CogTB.Length; iTB++)
                    if (m_CogTB[iTB] != null && m_CogTB[iTB].Subject != null)
                    {
                        if (m_CogTB[iTB].Subject.Inputs.Contains("NumberPoint"))
                        {
                            m_CogTB[iTB].Subject.Inputs["NumberPoint"].Value = ClassSystemConfig.Ins.m_ClsModelConfig.NumberPoints;
                        }
                        if (m_CogTB[iTB].Subject.Inputs.Contains("Mode"))
                        {
                            m_CogTB[iTB].Subject.Inputs["Mode"].Value = ClassSystemConfig.Ins.m_ClsModelConfig.MeasureMode;
                        }

                        // setpec
                        if (ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec != null && ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec.Count > 0)
                        {
                            string strGradingSpec = "";
                            for (int i = 0; i < ClassCommon.MaxGradeNumber; i++)
                            {
                                if (ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec[i] == null)
                                {
                                    strGradingSpec += string.Format("{0},{1},{2},", i + 1, 0, 0);
                                }
                                else
                                    if (ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec[i].Length == 1)
                                    {
                                        strGradingSpec += string.Format("{0},{1},{2},", i + 1, ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec[i][0], 0);
                                    }
                                    else
                                        if (ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec[i].Length >= 2)
                                        {
                                            strGradingSpec += string.Format("{0},{1},{2},", i + 1, ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec[i][0], ClassSystemConfig.Ins.m_ClsModelConfig.ListGradeSpec[i][1]);
                                        }

                            }

                            if (m_CogTB[iTB].Subject.Inputs.Contains("StringSpec"))
                            {
                                m_CogTB[iTB].Subject.Inputs["StringSpec"].Value = strGradingSpec;
                            }
                        }


                    }
            }
            catch { }
        }

        private void cbBoxListRecipes_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        int m_iTabSelectedIndex = -1;
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                tabControl1.SelectedIndex = 0;
                m_iTabSelectedIndex = 0;
                cbBoxInspectType.SelectedIndex = m_iTabSelectedIndex;
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                tabControl1.SelectedIndex = 1;
                m_iTabSelectedIndex = 1;
                cbBoxInspectType.SelectedIndex = m_iTabSelectedIndex;
            }
        }
        //private void radioButton3_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (radioButton3.Checked)
        //    {
        //        tabControl1.SelectedIndex = 2;
        //        m_iTabSelectedIndex = 2;
        //        cbBoxInspectType.SelectedIndex = m_iTabSelectedIndex;
        //    }
        //}
        private void btnRun_Click(object sender, EventArgs e)
        {
            if (m_iTabSelectedIndex >= 0 && m_iTabSelectedIndex < m_CogTB.Length)
            {
                if (m_CogTB != null && m_CogTB[m_iTabSelectedIndex] != null && m_CogTB[m_iTabSelectedIndex].Subject != null)
                    m_CogTB[m_iTabSelectedIndex].Subject.Run();
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            if (ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName != null)
            {
                string strRecipe = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName.Trim();
                if (MessageBox.Show("Do you want to load this model?", strRecipe + "//" + "RECIPE" + (m_iTabSelectedIndex + 1), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                {
                    bool bLoad = true;

                    ClassSystemConfig.Ins.m_ClsCommon.StartLoadingForm();
                    CheckCopyNewRecipe(strRecipe);
                    DateTime timeBeginLoad = DateTime.Now;

                    bLoad &= LoadModel(m_CogTB[m_iTabSelectedIndex], m_iTabSelectedIndex, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath, strRecipe, true);

                    var time_Loading = (DateTime.Now - timeBeginLoad).TotalMilliseconds;
                    if (time_Loading < 100)
                        Thread.Sleep(100);

                    ClassSystemConfig.Ins.m_ClsCommon.StopLoadingForm();

                    if (bLoad)
                    {
                        main.SaveDeviceConfig(false);
                        ClassSystemConfig.Ins.m_UserLog.SetLogOnline("Load Recipe \'" + strRecipe + "//" + "RECIPE" + (m_iTabSelectedIndex + 1) + "\' Done");
                    }

                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName != null)
            {
                string strRecipe = ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName.Trim();
                if (MessageBox.Show("Do you want to Save this model?", strRecipe + "//" + "RECIPE" + (m_iTabSelectedIndex + 1), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                {

                    SaveModel(m_CogTB[m_iTabSelectedIndex],
                                   m_iTabSelectedIndex,
                                   ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath,
                                   strRecipe,
                                   true
                                   );

                    ClassCommon.ShowMessageBox("Save Done", "Save Recipe : " + strRecipe + "//" + "RECIPE" + (m_iTabSelectedIndex + 1));
                }
            }
        }



        #region Edit resize 1

        #endregion
    }
}
