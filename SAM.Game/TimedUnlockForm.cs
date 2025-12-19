using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SAM.API;
namespace SAM.Game
{
    internal partial class TimedUnlockForm : Form
    {
        private readonly Client _SteamClient;
        private readonly List<Stats.AchievementInfo> _Achievements;
        private Timer _UnlockTimer;
        private int _RemainingSeconds = 0;
        private int _CurrentIndex = 0;
        public TimedUnlockForm(Client client, List<Stats.AchievementInfo> achievements)
        {
            InitializeComponent();
            _SteamClient = client;
            _Achievements = achievements;
           
            InitializeGrid();
        }
        private void InitializeGrid()
        {
            foreach (var achievement in _Achievements)
            {
                int rowIndex = _AchievementGrid.Rows.Add();
                var row = _AchievementGrid.Rows[rowIndex];
                row.Cells["AchievementName"].Value = achievement.Name;
                row.Cells["AchievementID"].Value = achievement.Id;
                row.Cells["Delay"].Value = 1; // Default 1 minute
                row.Cells["Status"].Value = "Pending";
                row.Tag = achievement;
            }
        }
        private void OnStart(object sender, EventArgs e)
        {
            if (_UnlockTimer != null && _UnlockTimer.Enabled)
            {
                _UnlockTimer.Stop();
                _StartButton.Text = "Resume";
                _StatusLabel.Text = "Paused.";
                return;
            }
            if (_CurrentIndex >= _AchievementGrid.Rows.Count)
            {
                MessageBox.Show("All achievements processed!");
                return;
            }
            _StartButton.Text = "Pause";
            _StatusLabel.Text = "Running...";
           
             // Start or Resume
            if (_RemainingSeconds > 0)
            {
                 // Resume existing timer
                _UnlockTimer.Start();
            }
            else
            {
                 // Start next one
                 StartNextTimer();
            }
        }
        private void StartNextTimer()
        {
            if (_CurrentIndex >= _AchievementGrid.Rows.Count)
            {
                _StatusLabel.Text = "Finished!";
                _StartButton.Text = "Start";
                _StartButton.Enabled = false;
                MessageBox.Show("All scheduled achievements have been unlocked!");
                return;
            }
            var row = _AchievementGrid.Rows[_CurrentIndex];
            row.Cells["Status"].Value = "Waiting...";
            row.Selected = true;
            int delayMinutes = 1;
            if (row.Cells["Delay"].Value != null && int.TryParse(row.Cells["Delay"].Value.ToString(), out int parsedDelay))
            {
                delayMinutes = parsedDelay;
            }
            _RemainingSeconds = delayMinutes * 60;
           
            // If user enters 0 or less, unlock immediately? Let's treat 0 as immediate.
            if (_RemainingSeconds <= 0)
            {
                UnlockCurrent();
                return;
            }
            _UnlockTimer = new Timer();
            _UnlockTimer.Interval = 1000; // 1 second
            _UnlockTimer.Tick += OnTimerTick;
            _UnlockTimer.Start();
           
            UpdateStatusLabel();
        }
        private void UpdateStatusLabel()
        {
            var row = _AchievementGrid.Rows[_CurrentIndex];
            TimeSpan time = TimeSpan.FromSeconds(_RemainingSeconds);
            _StatusLabel.Text = $"Unlocking '{row.Cells["AchievementName"].Value}' in {time.ToString(@"mm\:ss")}...";
        }
        private void OnTimerTick(object sender, EventArgs e)
        {
            _RemainingSeconds--;
           
            if (_RemainingSeconds <= 0)
            {
                _UnlockTimer.Stop();
                _UnlockTimer.Dispose();
                _UnlockTimer = null;
                UnlockCurrent();
            }
            else
            {
                UpdateStatusLabel();
            }
        }
        private void UnlockCurrent()
        {
            var row = _AchievementGrid.Rows[_CurrentIndex];
            var achievement = (Stats.AchievementInfo)row.Tag;
            try
            {
                if (_SteamClient.SteamUserStats.SetAchievement(achievement.Id, true))
                {
                    _SteamClient.SteamUserStats.StoreStats();
                    row.Cells["Status"].Value = "Unlocked";
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    row.Cells["Status"].Value = "Failed";
                    row.DefaultCellStyle.BackColor = Color.LightPink;
                }
            }
            catch (Exception ex)
            {
                row.Cells["Status"].Value = "Error";
                Console.Error.WriteLine(ex);
            }
            _CurrentIndex++;
            StartNextTimer();
        }
        private void OnMoveUp(object sender, EventArgs e)
        {
            if (_AchievementGrid.SelectedRows.Count == 0) return;
            var row = _AchievementGrid.SelectedRows[0];
            if (row.Index == 0) return;
            int index = row.Index;
            _AchievementGrid.Rows.RemoveAt(index);
            _AchievementGrid.Rows.Insert(index - 1, row);
            row.Selected = true;
        }
        private void OnMoveDown(object sender, EventArgs e)
        {
            if (_AchievementGrid.SelectedRows.Count == 0) return;
            var row = _AchievementGrid.SelectedRows[0];
            if (row.Index == _AchievementGrid.Rows.Count - 1) return;
            int index = row.Index;
            _AchievementGrid.Rows.RemoveAt(index);
            _AchievementGrid.Rows.Insert(index + 1, row);
            row.Selected = true;
        }
    }
}
