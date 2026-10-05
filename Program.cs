using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

// 検証用ツール（git管理外）。emoji_cache.sqlite を読み取り専用で開き、絵文字を一覧表示する。
// 引数にフォルダか .sqlite のパスを渡すと、起動時にそれを開く。
namespace EmojiCacheViewer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new ViewerForm(args.Length > 0 ? args[0] : null));
        }
    }

    sealed class ViewerForm : Form
    {
        const int Thumb = 48;
        readonly ListView list = new() { Dock = DockStyle.Fill, View = View.LargeIcon, MultiSelect = false };
        readonly ImageList images = new() { ImageSize = new Size(Thumb, Thumb), ColorDepth = ColorDepth.Depth32Bit };
        readonly TextBox tbFilter = new() { Width = 260, PlaceholderText = "URLで絞り込み" };
        readonly Label lblStatus = new() { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
        readonly PictureBox preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(32, 32, 32) };
        readonly Label lblDetail = new() { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(4) };
        readonly List<Entry> entries = new();
        string? dbPath;
        int loadVersion;

        record Entry(string Url, long Size, int ImageIndex);

        public ViewerForm(string? initialPath)
        {
            Text = "emoji_cache ビューア（検証用）";
            Size = new Size(1000, 700);
            list.LargeImageList = images;

            var btnOpen = new Button { Text = "開く…", AutoSize = true };
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            top.Controls.AddRange(new Control[] { btnOpen, tbFilter, lblStatus });

            var side = new Panel { Dock = DockStyle.Right, Width = 280 };
            side.Controls.Add(preview);
            side.Controls.Add(lblDetail);

            Controls.Add(list);
            Controls.Add(side);
            Controls.Add(top);

            btnOpen.Click += (_, _) => Browse();
            tbFilter.TextChanged += (_, _) => Refill();
            list.SelectedIndexChanged += (_, _) => ShowSelected();

            if (initialPath != null) Shown += (_, _) => Open(initialPath);
        }

        void Browse()
        {
            using var ofd = new OpenFileDialog { Filter = "emoji_cache.sqlite|*.sqlite;*.db|All files|*.*" };
            if (ofd.ShowDialog(this) == DialogResult.OK) Open(ofd.FileName);
        }

        void Open(string path)
        {
            if (Directory.Exists(path)) path = Path.Combine(path, "emoji_cache.sqlite");
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "ファイルが見つかりません: " + path);
                return;
            }
            dbPath = path;
            Text = "emoji_cache ビューア（検証用） - " + path;
            LoadEntries();
        }

        async void LoadEntries()
        {
            var version = ++loadVersion;
            var path = dbPath!;
            entries.Clear();
            images.Images.Clear();
            list.Items.Clear();
            lblStatus.Text = "読み込み中...";
            try
            {
                var (loaded, failed) = await Task.Run(() => ReadAll(path, version));
                if (version != loadVersion) return;
                Refill();
                lblStatus.Text = $"{entries.Count} 件（画像として読めない: {failed} 件）";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "読み込み失敗: " + ex.Message;
            }
        }

        (int, int) ReadAll(string path, int version)
        {
            var thumbs = new List<Bitmap>();
            var rows = new List<(string Url, long Size)>();
            int failed = 0;
            using (var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT url, data FROM emoji_cache ORDER BY url";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (version != loadVersion) return (0, 0);
                    var url = r.GetString(0);
                    var data = (byte[])r.GetValue(1);
                    var thumb = MakeThumb(data);
                    if (thumb == null) { failed++; thumb = MakePlaceholder(); }
                    rows.Add((url, data.LongLength));
                    thumbs.Add(thumb);
                }
            }
            Invoke(() =>
            {
                if (version != loadVersion) return;
                images.Images.AddRange(thumbs.Cast<Image>().ToArray());
                for (int i = 0; i < rows.Count; i++) entries.Add(new Entry(rows[i].Url, rows[i].Size, i));
            });
            return (rows.Count, failed);
        }

        static Bitmap? MakeThumb(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var src = Image.FromStream(ms);
                var bmp = new Bitmap(Thumb, Thumb);
                using var g = Graphics.FromImage(bmp);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var scale = Math.Min((float)Thumb / src.Width, (float)Thumb / src.Height);
                var w = src.Width * scale; var h = src.Height * scale;
                g.DrawImage(src, (Thumb - w) / 2, (Thumb - h) / 2, w, h);
                return bmp;
            }
            catch { return null; }
        }

        static Bitmap MakePlaceholder()
        {
            var bmp = new Bitmap(Thumb, Thumb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.DimGray);
            g.DrawString("?", SystemFonts.DefaultFont, Brushes.White, 18, 17);
            return bmp;
        }

        void Refill()
        {
            var f = tbFilter.Text.Trim();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var e in entries)
            {
                if (f.Length > 0 && !e.Url.Contains(f, StringComparison.OrdinalIgnoreCase)) continue;
                var label = e.Url[(e.Url.LastIndexOf('/', Math.Max(0, e.Url.LastIndexOf('/') - 1)) + 1)..];
                list.Items.Add(new ListViewItem(label, e.ImageIndex) { Tag = e, ToolTipText = e.Url });
            }
            list.EndUpdate();
            if (entries.Count > 0) lblStatus.Text = $"{list.Items.Count} / {entries.Count} 件表示";
        }

        void ShowSelected()
        {
            if (list.SelectedItems.Count == 0 || dbPath == null) return;
            var e = (Entry)list.SelectedItems[0].Tag!;
            lblDetail.Text = $"{e.Url}\r\n{e.Size:N0} bytes";
            try
            {
                using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT data FROM emoji_cache WHERE url = $u";
                cmd.Parameters.AddWithValue("$u", e.Url);
                var data = (byte[]?)cmd.ExecuteScalar();
                var old = preview.Image;
                preview.Image = data == null ? null : new Bitmap(new MemoryStream(data));
                old?.Dispose();
            }
            catch { preview.Image = null; }
        }
    }
}
