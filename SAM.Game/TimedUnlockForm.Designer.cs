namespace SAM.Game
{
    partial class TimedUnlockForm
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            this._AchievementGrid = new System.Windows.Forms.DataGridView();
            this._StartButton = new System.Windows.Forms.Button();
            this._MoveUpButton = new System.Windows.Forms.Button();
            this._MoveDownButton = new System.Windows.Forms.Button();
            this._StatusLabel = new System.Windows.Forms.Label();
            this._ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._ColumnID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._ColumnDelay = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._ColumnStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this._AchievementGrid)).BeginInit();
            this.SuspendLayout();
            //
            // _AchievementGrid
            //
            this._AchievementGrid.AllowUserToAddRows = false;
            this._AchievementGrid.AllowUserToDeleteRows = false;
            this._AchievementGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this._AchievementGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._AchievementGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._ColumnName,
            this._ColumnID,
            this._ColumnDelay,
            this._ColumnStatus});
            this._AchievementGrid.Location = new System.Drawing.Point(12, 41);
            this._AchievementGrid.Name = "_AchievementGrid";
            this._AchievementGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._AchievementGrid.MultiSelect = false;
            this._AchievementGrid.Size = new System.Drawing.Size(560, 300);
            this._AchievementGrid.TabIndex = 0;
            //
            // _ColumnName
            //
            this._ColumnName.HeaderText = "Name";
            this._ColumnName.Name = "AchievementName";
            this._ColumnName.ReadOnly = true;
            this._ColumnName.Width = 200;
            //
            // _ColumnID
            //
            this._ColumnID.HeaderText = "ID";
            this._ColumnID.Name = "AchievementID";
            this._ColumnID.ReadOnly = true;
            this._ColumnID.Visible = false;
            //
            // _ColumnDelay
            //
            this._ColumnDelay.HeaderText = "Delay (min)";
            this._ColumnDelay.Name = "Delay";
            this._ColumnDelay.Width = 80;
            //
            // _ColumnStatus
            //
            this._ColumnStatus.HeaderText = "Status";
            this._ColumnStatus.Name = "Status";
            this._ColumnStatus.ReadOnly = true;
            this._ColumnStatus.Width = 100;
            //
            // _MoveUpButton
            //
            this._MoveUpButton.Location = new System.Drawing.Point(12, 12);
            this._MoveUpButton.Name = "_MoveUpButton";
            this._MoveUpButton.Size = new System.Drawing.Size(75, 23);
            this._MoveUpButton.TabIndex = 1;
            this._MoveUpButton.Text = "Move Up";
            this._MoveUpButton.UseVisualStyleBackColor = true;
            this._MoveUpButton.Click += new System.EventHandler(this.OnMoveUp);
            //
            // _MoveDownButton
            //
            this._MoveDownButton.Location = new System.Drawing.Point(93, 12);
            this._MoveDownButton.Name = "_MoveDownButton";
            this._MoveDownButton.Size = new System.Drawing.Size(75, 23);
            this._MoveDownButton.TabIndex = 2;
            this._MoveDownButton.Text = "Move Down";
            this._MoveDownButton.UseVisualStyleBackColor = true;
            this._MoveDownButton.Click += new System.EventHandler(this.OnMoveDown);
            //
            // _StartButton
            //
            this._StartButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._StartButton.Location = new System.Drawing.Point(497, 347);
            this._StartButton.Name = "_StartButton";
            this._StartButton.Size = new System.Drawing.Size(75, 23);
            this._StartButton.TabIndex = 3;
            this._StartButton.Text = "Start";
            this._StartButton.UseVisualStyleBackColor = true;
            this._StartButton.Click += new System.EventHandler(this.OnStart);
            //
            // _StatusLabel
            //
            this._StatusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._StatusLabel.AutoSize = true;
            this._StatusLabel.Location = new System.Drawing.Point(12, 352);
            this._StatusLabel.Name = "_StatusLabel";
            this._StatusLabel.Size = new System.Drawing.Size(38, 13);
            this._StatusLabel.TabIndex = 4;
            this._StatusLabel.Text = "Ready";
            //
            // TimedUnlockForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 382);
            this.Controls.Add(this._StatusLabel);
            this.Controls.Add(this._StartButton);
            this.Controls.Add(this._MoveDownButton);
            this.Controls.Add(this._MoveUpButton);
            this.Controls.Add(this._AchievementGrid);
            this.Name = "TimedUnlockForm";
            this.Text = "Timed Achievement Unlocker";
            ((System.ComponentModel.ISupportInitialize)(this._AchievementGrid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        private System.Windows.Forms.DataGridView _AchievementGrid;
        private System.Windows.Forms.Button _StartButton;
        private System.Windows.Forms.Button _MoveUpButton;
        private System.Windows.Forms.Button _MoveDownButton;
        private System.Windows.Forms.Label _StatusLabel;
        private System.Windows.Forms.DataGridViewTextBoxColumn _ColumnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn _ColumnID;
        private System.Windows.Forms.DataGridViewTextBoxColumn _ColumnDelay;
        private System.Windows.Forms.DataGridViewTextBoxColumn _ColumnStatus;
    }
}
