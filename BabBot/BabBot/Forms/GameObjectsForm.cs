﻿/*
    This file is part of BabBot.

    BabBot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    BabBot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with BabBot.  If not, see <http://www.gnu.org/licenses/>.
  
    Copyright 2009 BabBot Team
*/

// TODO
// Test Global Save with quests added
// Add new form to add new npc/object with min parameters
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using BabBot.Manager;
using System.IO;
using BabBot.Wow;
using BabBot.Data;
using BabBot.Common;
using BabBot.Wow.Helpers;

namespace BabBot.Forms
{
    public partial class GameObjectsForm : BabBot.Forms.GenericDialog
    {
        private readonly string[] ReqSrvDescr = new string[] {
            "inn", "taxi", "class_trainer", "trade_skill_trainer", "wep_skill_trainer" };

        private string LogFacility = "game_objects";

        protected override bool IsChanged
        {
            set
            {
                base.IsChanged = value;

                if (value)
                {
                    // Change button text
                    btnClose.Text = "Cancel";

                    // Mark current record as changed
                    SetCurrentRowModified();
                }
                else
                    btnClose.Text = "Close";
            }
        }

        public GameObjectsForm() : base ("npc_list")
        {
            InitializeComponent();

            // Dictionary tables first
            bsZoneList.DataSource = DataManager.GameData;
            bsServiceTypesFull.DataSource = DataManager.GameData;
            bsCoordTypes.DataSource = DataManager.GameData;

            bsGameObjects.DataSource = DataManager.GameData;
            bsServiceTypesFiltered.DataSource = DataManager.GameData;

#if DEBUG
            if (ProcessManager.Config.Test == 1)
            {
                labelTitle.Visible = false;

                tbX.Text = "1.1";
                tbY.Text = "2.2";
                tbZ.Text = "3.3";
            }
#endif
        }

        public void Open()
        {
            labelWoWVersion.Text = DataManager.CurWoWVersion.Build;

            IsChanged = false;
            Show();
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { RestoreDirectory = true, Multiselect = true, 
                Filter = "Game Object data files (*.obj)|*.obj" };
            dlg.InitialDirectory = "Data" + Path.DirectorySeparatorChar + "Import";

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            // Start NPC channel and switch to main form
            Output.Instance.Log(LogFacility, "Starting data import ...");
            Program.mainForm.SelectLogTab(LogFacility);
            List<GameObject> list = new List<GameObject>();
            foreach (string fname in dlg.FileNames)
            {
                try
                {
                    GameObject g = DataManager.LoadGameObj(fname);
                    g.Changed = false;

                    list.Add(g);

                    // Add game object to dataset
                    DataManager.AddGameObject(g);

                    // Select new data
                    SelectGameObj(g);
                }
                catch
                {
                    ShowErrorMessage("Failed import data from " + fname +
                                            ". Check format and try again");
                }
            }

            // Save & Index data after all imports done
            // Export not required since it's already external files
            if ((list.Count > 0) && 
                DataManager.MergeGameObjData(list, false, LogFacility))
                    DataManager.GameData.AcceptChanges();
                    
        }

        protected override void OnFormClosing(
                        object sender, FormClosingEventArgs e)
        {
            // For some reason called twice
            if (!e.Cancel)
                base.OnFormClosing(sender, e);

            if (!e.Cancel)
            {
                if (!btnClose.Text.Equals("Close"))
                {
                    DataManager.GameData.RejectChanges();
                    btnClose.Text = "Close";
                }

                e.Cancel = true; // this cancels the close event.
                Visible = false;
                // Hide();
            }
        }

        void SetFormControls(bool Enabled)
        {
            gbDescription.Enabled = Enabled;
        }

        private bool CheckBeforeNpcTest()
        {
            if (!CheckTarget())
                return false;

            // Start NPC channel and switch to main form
            Output.Instance.Log("npc", "Starting npc test ...");
            Program.mainForm.SelectLogTab("npc");

            return true;
        }

        internal void btnAddNPC_Click(object sender, EventArgs e)
        {
            if (!CheckBeforeNpcTest())
                return;

#if DEBUG
            //\\ TEST
            if (ProcessManager.Config.IsTest)
                if ((ProcessManager.Config.Test == 1) &&
                    !ProcessManager.Player.HasTarget)
                {
                    // Target NPC
                    // string name = "Melithar Staghelm";
                    // string name = "Conservator Ilthalaine";
                    // string name = "Gilshalan Windwalker";
                    string name = "Dellylah";
                    LuaHelper.TargetUnitByName(name);
                }
#endif

            try
            {
                GameObject npc = NpcHelper.AddNpc("npc");
                if (npc != null)
                {
                    // Check for duplication
                    int idx = bsGameObjects.Find("NAME", npc.Name);
                    if (idx >= 0)
                        ((DataView)((DataRowView)bsGameObjects.Current).DataView).Delete(idx);
                    DataManager.AddGameObject(npc);
                    SelectGameObj(npc);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Can't add current NPC. " + ex.Message);
            }
        }

        private void SelectGameObj(GameObject obj)
        {
            lbGameObjList.SelectedIndex = bsGameObjects.Find("NAME", obj.Name);
        }

        private bool CheckGameObjSelected()
        {
            if (lbGameObjList.SelectedItem == null)
            {
                ShowErrorMessage("No NPC Selected");
                return false;
            }

            return true;
        }

        private void moveToObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!CheckGameObjSelected())
                return;
            
            if (!CheckInGame())
                return;

            try
            {
                NpcHelper.MoveToGameObjByName(GetCurrentRow().NAME, "npc");
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void btnMoveToNearest_Click(object sender, EventArgs e)
        {
            if (!CheckInGame())
                return;

            if (cbServiceList.SelectedItem == null)
            {
                ShowErrorMessage("No services selected");
                return;
            }

            // Find nearest class trainer
            try
            {
                BotDataSet.ServiceTypesRow srv_row = (BotDataSet.ServiceTypesRow)
                                            ((DataRowView)cbServiceList.SelectedItem).Row;
                btnMoveToNearest.Enabled = false;
                NPC npc = NpcHelper.MoveInteractService(srv_row.NAME, "npc");

                // Select found npc
                if (npc != null)
                    SelectGameObj(npc);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
            finally
            {
                btnMoveToNearest.Enabled = true;
            }
        }

        #region DEBUG
        
        private void LearnNpcSkills(NPC npc)
        {
            // Learn all skills
            string skill = cbServiceList.SelectedItem.ToString();
            NpcHelper.LearnSkills(npc, skill, "npc");
        }

        #endregion

        private void cbServiceList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbServiceList.SelectedItem == null)
                return;

            string skill = cbServiceList.SelectedItem.ToString();
            // btnLearnSkill.Enabled = (skill.Equals("class_trainer") || skill.Equals("wep_skill_trainer"));
            btnMoveToNearest.Enabled = cbServiceList.SelectedItem != null;
        }

        private void cbUseState_CheckedChanged(object sender, EventArgs e)
        {
            NpcHelper.UseState = cbUseState.Checked;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            if (IsChanged && (MessageBox.Show("Are you sure cancel changes ?",
                        "Confirmation", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation) == DialogResult.No))
                return;
            else
            {
                BotDataSet.GameObjectsRow row = GetCurrentRow();
                // row.CancelEdit();
                DataManager.GameData.RejectChanges();
            }

            if (btnClose.Text.Equals("Close"))
                Hide();

            IsChanged = false;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if ((tbDescr.Enabled) && tbDescr.Text.Equals(""))
            {
                ShowErrorMessage("Description is required for selected service\n" +
                    "i.e class name for class_trainer and so on");
                return;
            }

            DataRowView srv_row = (DataRowView)bsServiceTypesFiltered.Current;

            if (srv_row == null)
                return;

            BotDataSet.ServiceTypesRow srow = (BotDataSet.ServiceTypesRow) srv_row.Row;
            BotDataSet.GameObjectsRow cur_row = GetCurrentRow();

            DataManager.GameData.NpcServices.AddNpcServicesRow(cur_row, srow, srow.NAME, tbDescr.Text);

            IsChanged = true;
        }

        /// <summary>
        /// Change state of current GameObject Row to modified
        /// </summary>
        /// <param name="row">Current GameObject row</param>
        private void SetCurrentRowModified()
        {
            BotDataSet.GameObjectsRow row = GetCurrentRow();

            if (row.RowState == DataRowState.Unchanged)
                row.SetModified();
        }

        private void lbActiveServices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedNpcService();
                e.Handled = true;
            }
        }

        private void DeleteSelectedNpcService()
        {
            if (lbActiveServices.SelectedItem == null)
                return;

            ((DataRowView)fkGameObjectsNpcServices.Current).Delete();

            IsChanged = true;
        }

        private void deleteServiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelectedNpcService();
        }

        private void btnAddNewObj_Click(object sender, EventArgs e)
        {
            // TODO
            ShowErrorMessage("Currently supported only in-game adding of new NPC/Game Object");
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Process new rows
                DataTable tbl = DataManager.GameData.GameObjects.
                                            GetChanges(DataRowState.Added);
                if (tbl != null)
                    foreach (BotDataSet.GameObjectsRow row in tbl.Rows)
                        DataManager.AddGameObjectRow(row);

                // Process changed rows
                tbl = DataManager.GameData.GameObjects.GetChanges(DataRowState.Modified);
                if (tbl != null)
                    foreach (BotDataSet.GameObjectsRow row in tbl.Rows)
                        DataManager.SaveGameObjRow(row);

                // Processed deleted rows
                tbl = DataManager.GameData.GameObjects.GetChanges(DataRowState.Deleted);
                if (tbl != null)
                {
                    // A bit tricky with deleted rows
                    DataView dv = new DataView(tbl, null, null, DataViewRowState.Deleted);
                    //The new DataTable (dt) now contains the original versions of the deleted rows.
                    tbl = dv.ToTable();
                    foreach (DataRow row in tbl.Rows)
                        DataManager.DeleteGameObjRow(row["NAME"].ToString());
                }

                // Save data on the disk
                if (DataManager.SaveGameObjData(LogFacility))
                {
                    // Than accept changes
                    DataManager.GameData.AcceptChanges();

                    IsChanged = false;

                    ShowInfoMessage("Game Object Data successfully saved !!!");
                }
            }
            catch (Exception ex)
            {
                // Keep edit state
                ShowErrorMessage(ex);
            }
        }

        #region GameObjects

        private void gameObjectsBindingSource_CurrentChanged(object sender, EventArgs e)
        {
            BotDataSet.GameObjectsRow row = GetCurrentRow();
            if (row == null)
                return;

            cbPlayerTarget.SelectedIndex = row.TYPE_ID;

            // Clear description
            tbDescr.Text = "";
        }

        private void lbGameObjectList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedObject();
                e.Handled = true;
            }
        }

        private void deleteGameObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelectedObject();
        }

        private void DeleteSelectedObject()
        {
            if (!CheckGameObjSelected())
                return;

            // Possible multi selection
            DataRowView obj = (DataRowView)lbGameObjList.SelectedItem;
            string obj_name = obj.Row["NAME"].ToString();
            if (MessageBox.Show(this, "Are you sure to delete " + obj_name + "?",
                    "Confirmation", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // Delete from all version or it will merge again
                obj.Delete();
                IsChanged = true;
            }
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (btnSave.Enabled)
            {
                ShowErrorMessage("Game Object in Edit Mode. Save data and try again");
                return;
            }

            BotDataSet.GameObjectsRow row = GetCurrentRow();
            GameObject obj = DataManager.CurWoWVersion.GameObjData.STable[row.NAME];
            if (obj == null)
            {
                ShowErrorMessage("Game Object not found. Restart Bot and try again");
                return;
            }

            string res = DataManager.ExportGameObj(obj, null);
            if (res != null)
                ShowInfoMessage("Game Object successfully exported to file '" + res +
                    "'.\nDon't forget submit updated data (if any) to " +
                        "BabBot forum https://sourceforge.net/apps/phpbb/babbot/");

        }

        #endregion

        #region Services

        private void popServiceActions_Opening(object sender, CancelEventArgs e)
        {
            deleteServiceToolStripMenuItem.Enabled =
                    (lbActiveServices.SelectedItem != null);
        }

        #endregion

        #region Quests

        private void popQuestActions_Opening(object sender, CancelEventArgs e)
        {
            deleteQuestToolStripMenuItem.Enabled = 
                    (lbQuestList.SelectedItems.Count > 0);
        }

        private void deleteQuestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelectedQuest();
        }

        private void lbQuestList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedQuest();
                e.Handled = true;
            }
        }

        public void DeleteSelectedQuest()
        {
            if (lbQuestList.SelectedItem == null)
                return;

            ((DataRowView)fkGameObjectsQuestList.Current).Delete();

            IsChanged = true;
        }

        private void lbQuestList_DoubleClick(object sender, EventArgs e)
        {
            // TODO
            ShowErrorMessage("Quest Form not implemented yet");
        }

        private void btnAddQuest_Click(object sender, EventArgs e)
        {
            // TODO
            ShowErrorMessage("Quest Form not implemented yet");

            // Dont forget set parent row modified
            // BotDataSet.GameObjectsRow cur_row = GetCurrentRow();
            // SetCurrentModified(cur_row);
        }

        private Quest CheckBeforeQuestTest()
        {
            if (!CheckInGame())
                return null;

            if (lbQuestList.SelectedItem == null)
            {
                ShowErrorMessage("No quest selected");
                return null;
            }

            BotDataSet.QuestListRow qrow = (BotDataSet.QuestListRow)
                                ((DataRowView)lbQuestList.SelectedItem).Row;
            int qid = qrow.ID;

            Quest q = DataManager.QuestList[qid];

            if (q == null)
            {
                ShowErrorMessage("Quest '" + qrow.TITLE + "' not found");
                return null;
            }

            // Start NPC channel and switch to main form
            Output.Instance.Log("quest_test", "Starting quest test ...");
            Program.mainForm.SelectLogTab("quest_test");

            return q;
        }

        private void acceptQuestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Quest q = CheckBeforeQuestTest();
            if (q == null)
                return;

            try
            {
                QuestHelper.AcceptQuest(q, "quest_test");
            }
            catch (QuestProcessingException qe)
            {
                ShowErrorMessage("Quest processing error: " + qe.Message);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void deliverQuestToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        #endregion

        #region Coordinates

        private void deleteCoordinatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelectedCoordinates();
        }

        private void lbCoordinates_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedCoordinates();
                e.Handled = true;
            }
        }

        private void DeleteSelectedCoordinates()
        {
            if (lbCoordinates.SelectedItem == null)
                return;
            else if ((cbCoordZone.Items.Count == 1) && (lbCoordinates.Items.Count == 1))
            {
                ShowErrorMessage("The base object coordinates cannot be deleted");
                return;
            }

            ((DataRowView)fKCoordinatesZoneCoordinates.Current).Delete();
            
            // If no other coordinates delete zone too
            if (fKCoordinatesZoneCoordinates.Count == 0)
                ((DataRowView)((BindingSource)fKCoordinatesZoneCoordinates.DataSource).Current).Delete();

            IsChanged = true;
        }

        private void popCoordinates_Opening(object sender, CancelEventArgs e)
        {
            deleteCoordinatesToolStripMenuItem.Enabled =
                            (lbCoordinates.SelectedItem != null);
        }

        private void btnAddPlayerTargetCoord_Click(object sender, EventArgs e)
        {
            if (cbPlayerTarget.SelectedIndex == 0)
                // Add player coord
                AddAsPlayerCoord();
            else if (cbPlayerTarget.SelectedIndex == 1)
                AddAsTargetCoord();

        }

        private void AddAsPlayerCoord()
        {
            if (!CheckInGame())
                return;

            AddGameObjCoord(ProcessManager.Player.ZoneText, 
                                            ProcessManager.Player.Location);
        }

        private void AddAsTargetCoord()
        {
            if (!CheckTarget())
                return;

            AddGameObjCoord(ProcessManager.Player.ZoneText, 
                                    ProcessManager.Player.CurTarget.Location);
        }

        private void btnAddCoord_Click(object sender, EventArgs e)
        {
            if (cbAllZones.SelectedItem == null)
            {
                ShowErrorMessage("Zone not selected");
                return;
            }

            // Remember new zone name
            string zone_name = cbAllZones.Text;
            try
            {
                AddGameObjCoord(cbAllZones.Text, Convert.ToDouble(tbX.Text),
                    Convert.ToDouble(tbY.Text), Convert.ToDouble(tbZ.Text), 
                    ((BotDataSet.CoordTypesRow) ((DataRowView)cbCoordType.SelectedItem).Row).ID);

                // Set GameObject Zone List to new added zone coordinates
                DataView view = ((DataRowView)fKGameObjectsCoordinatesZone.Current).DataView;

                // Set Zone List to same zone as current GameObject coordinates
                cbCoordZone.SelectedItem = view[view.Find(zone_name)];

                tbX.Text = String.Empty;
                tbY.Text = String.Empty;
                tbZ.Text = String.Empty;
#if DEBUG
                if (ProcessManager.Config.Test == 1)
                {
                    tbX.Text = "1.1";
                    tbY.Text = "2.2";
                    tbZ.Text = "3.3";
                }
#endif
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void AddGameObjCoord(string zone, Vector3D v)
        {
            AddGameObjCoord(zone, v.X, v.Y, v.Z, v.Type);
        }

        private void AddGameObjCoord(string zone, double x, double y, double z, int type)
        {
            BotDataSet.GameObjectsRow cur_row = GetCurrentRow();

            // Check if new coord not too close
            DataRow[] crows = new DataRow[0];

            DataRow[] zrows = DataManager.GameData.CoordinatesZone.Select("GID=" + cur_row.ID);
            foreach (BotDataSet.CoordinatesZoneRow zrow in zrows)
            {
                DataRow[] coords = DataManager.GameData.Coordinates.Select("ZONE_ID=" + zrow.ID);
                int old_idx = crows.Length;
                Array.Resize<DataRow>(ref crows, crows.Length + coords.Length);
                coords.CopyTo(crows, old_idx);
            }

            foreach (BotDataSet.CoordinatesRow row in crows)
            {
                if (Math.Sqrt(Math.Pow((double)(row.X - x), 2) + 
                        Math.Pow((double)(row.Y - y), 2) +
                        Math.Pow((double)(row.Z - z), 2)) < 5)
                {
                    ShowErrorMessage("New coordinates located in less than 5 yards with [" +
                        row.COORD + "]");
                    return;
                }
            }

            // Check if zone exists
            object zx = cbCoordZone.Items;
            DataView view = ((DataRowView)fKGameObjectsCoordinatesZone.Current).DataView;
            
            DataRowView[] rows = view.FindRows(zone);

            BotDataSet.CoordinatesZoneRow zone_row = null;
            if (rows.Length == 0)
                zone_row = DataManager.GameData.CoordinatesZone.
                    AddCoordinatesZoneRow(GetCurrentRow(), zone);
            else
                zone_row = (BotDataSet.CoordinatesZoneRow) rows[0].Row;

            DataManager.GameData.Coordinates.AddCoordinatesRow(zone_row, x, y, z, type);

            // Select last row
            lbCoordinates.SelectedIndex = lbCoordinates.Items.Count - 1;

            IsChanged = true;
        }

        #endregion

        private BotDataSet.GameObjectsRow GetCurrentRow()
        {
            DataRowView rview = (DataRowView)bsGameObjects.Current;
            if (rview == null)
                return null;

            return  (BotDataSet.GameObjectsRow) rview.Row;

        }

        private void bsFKGameObjectsNpcServices_ListChanged(object sender, ListChangedEventArgs e)
        {
            // Set filter on available services
            BotDataSet.GameObjectsRow current = GetCurrentRow();
            DataRowView srv = (DataRowView)fkGameObjectsNpcServices.Current;
            bsServiceTypesFiltered.Filter = "";

            if (srv == null)
                bsServiceTypesFiltered.RemoveFilter();
            else
            {
                DataRow[] cur_srv = DataManager.
                    GameData.NpcServices.Select("GID=" + current.ID);

                if (cur_srv.Length > 0)
                {
                    string filter = "ID NOT IN (" + cur_srv[0]["SERVICE_ID"];
                    for (int i = 1; i < cur_srv.Length; i++)
                        filter += "," + cur_srv[i]["SERVICE_ID"];
                    bsServiceTypesFiltered.Filter = filter + ")";
                }
            }
        }

        private void btnAddItem_Click(object sender, EventArgs e)
        {
            ShowErrorMessage("Not implemented yet");
        }

        private void GameObjectsForm_Activated(object sender, EventArgs e)
        {
            SetControls(lbGameObjList.Items.Count > 0);
        }

        private void SetControls(bool enabled)
        {
            gbAddCoord.Enabled = enabled;
            gbCoordinates.Enabled = enabled;
            gbDescription.Enabled = enabled;
            gbQuestList.Enabled = enabled;
            gbAutoAdd.Enabled = enabled;
        }

        private void cbCoordZone_SelectedIndexChanged(object sender, EventArgs e)
        {
            // cbAllZones.Text = cbCoordZone.Text;
        }

        private void popGameObject_Opening(object sender, CancelEventArgs e)
        {
            moveToObjectToolStripMenuItem.Text = "Move To";
            deleteGameObjectToolStripMenuItem.Text = "Delete";

            if (!CheckGameObjSelected())
                return;

            moveToObjectToolStripMenuItem.Text += " '" + GetCurrentRow().NAME + "'";
            deleteGameObjectToolStripMenuItem.Text += " '" + GetCurrentRow().NAME + "'";
        }

        private void bsCoordTypes_CurrentChanged(object sender, EventArgs e)
        {
            if (((BotDataSet.CoordTypesRow) ((DataRowView)bsCoordTypes.Current).Row).ID != 0)
            {
                ShowErrorMessage("Relative coordinates not supported yet.");
                cbCoordType.SelectedItem = ((DataRowView)bsCoordTypes.Current).DataView[0];
                // bsCoordTypes.Current =
            }
        }

        private void fKGameObjectsCoordinatesZone_CurrentChanged(object sender, EventArgs e)
        {
            if ((fKGameObjectsCoordinatesZone.Current == null) || (bsZoneList.Current == null))
                return;

            // Get current row
            BotDataSet.CoordinatesZoneRow row = (BotDataSet.CoordinatesZoneRow)
                                ((DataRowView)fKGameObjectsCoordinatesZone.Current).Row;

            // Get Zone List Data View
            DataView view = ((DataRowView)bsZoneList.Current).DataView;

            // Set Zone List to same zone as current GameObject coordinates
            cbAllZones.SelectedItem = view[view.Find(row.ZONE_NAME)];
        }

        private void bsServiceTypesFiltered_CurrentChanged(object sender, EventArgs e)
        {
            BotDataSet.ServiceTypesRow row = null;
            if (bsServiceTypesFiltered.Current != null)
                row = (BotDataSet.ServiceTypesRow)((DataRowView)bsServiceTypesFiltered.Current).Row;

            tbDescr.Enabled = ((row != null) &&
                Array.IndexOf(ReqSrvDescr, row.NAME) >= 0);

            // Clear description
            tbDescr.Text = "";
        }
    }
}