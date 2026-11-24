using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SandpileSimulator;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    private int[,] field = new int[1000, 1000];
    private int width = 1000, height = 1000;
    private long lost = 0, totalSteps = 0;
    private bool running = false, finishMode = false;

    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30 };
    private readonly PictureBox picture = new() { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label status = new() { AutoSize = true, ForeColor = Color.White };
    private readonly Label stats = new() { AutoSize = true, ForeColor = Color.White };
    private readonly Panel graph = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 24) };
    private readonly List<int> history = new();

    private NumericUpDown widthBox = new();
    private NumericUpDown heightBox = new();
    private NumericUpDown stonesBox = new();

    private int activeCells = 0;
    private Bitmap? currentBitmap;

    public MainForm()
    {
        Text = "Sandpile Simulator";
        Width = 1250;
        Height = 820;
        MinimumSize = new Size(900, 600);
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.White;

        BuildInterface();
        ResetField();
        timer.Tick += TimerTick;
    }

    private void BuildInterface()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
        Controls.Add(layout);

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(36, 36, 42)
        };
        layout.Controls.Add(left, 0, 0);

        left.Controls.Add(Header("ПАРАМЕТРЫ ПОЛЯ"));
        widthBox = NumberBox(1000, 10, 3000);
        heightBox = NumberBox(1000, 10, 3000);
        stonesBox = NumberBox(1000, 1, 100000000);

        AddField(left, "Ширина:", widthBox);
        AddField(left, "Высота:", heightBox);
        AddField(left, "Камушков:", stonesBox);

        left.Controls.Add(MakeButton("Создать / сбросить поле", (_, _) => ResetField()));
        left.Controls.Add(Header("УПРАВЛЕНИЕ"));
        left.Controls.Add(MakeButton("▶ Запуск", (_, _) => Start()));
        left.Controls.Add(MakeButton("Ⅱ Пауза", (_, _) => Pause()));
        left.Controls.Add(MakeButton("Один шаг", (_, _) => OneStep()));
        left.Controls.Add(MakeButton("До завершения", (_, _) => Finish()));
        left.Controls.Add(MakeButton("Сохранить PNG", (_, _) => SaveImage()));

        var center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5), BackColor = Color.Black };
        center.Controls.Add(picture);
        layout.Controls.Add(center, 1, 0);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(36, 36, 42)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(right, 2, 0);
        right.Controls.Add(Header("СТАТИСТИКА"), 0, 0);
        right.Controls.Add(stats, 0, 1);
        right.Controls.Add(graph, 0, 2);
        graph.Paint += DrawGraph;

        var legend = new Label
        {
            Text = "ЦВЕТА КЛЕТОК\n\n■ 0 — чёрный\n■ 1 — медный\n■ 2 — зелёный\n■ 3 — белый",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(36, 36, 42),
            AutoSize = true,
            Padding = new Padding(5)
        };
        left.Controls.Add(legend);

        status.Text = "Готово";
        status.ForeColor = Color.LightGreen;
        status.Padding = new Padding(3);
        left.Controls.Add(status);
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.FromArgb(255, 170, 90),
        Font = new Font("Segoe UI", 11, FontStyle.Bold),
        Margin = new Padding(3, 10, 3, 8)
    };

    private static NumericUpDown NumberBox(int value, int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        Width = 190,
        BackColor = Color.FromArgb(55, 55, 62),
        ForeColor = Color.White
    };

    private static void AddField(Control parent, string title, Control control)
    {
        parent.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = Color.White,
            Margin = new Padding(3, 8, 3, 2)
        });
        parent.Controls.Add(control);
    }

    private static Button MakeButton(string text, EventHandler action)
    {
        var button = new Button
        {
            Text = text,
            Width = 210,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(65, 65, 75),
            ForeColor = Color.White,
            Margin = new Padding(3, 5, 3, 5)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 110);
        button.Click += action;
        return button;
    }

    private void ResetField()
    {
        Pause();
        width = (int)widthBox.Value;
        height = (int)heightBox.Value;
        field = new int[height, width];

        int x = width / 2;
        int y = height / 2;
        field[y, x] = (int)stonesBox.Value;

        lost = 0;
        totalSteps = 0;
        history.Clear();
        CountActive();
        RenderField();
        UpdateStats();
        status.Text = "Новое поле создано";
    }

    private void Start()
    {
        if (activeCells == 0) return;
        finishMode = false;
        running = true;
        timer.Start();
        status.Text = "Симуляция выполняется";
    }

    private void Pause()
    {
        running = false;
        timer.Stop();
        status.Text = "Пауза";
    }

    private void OneStep()
    {
        Pause();
        if (activeCells > 0)
        {
            Step();
            RenderField();
            UpdateStats();
        }
    }

    private void Finish()
    {
        if (activeCells == 0) return;
        finishMode = true;
        running = true;
        timer.Start();
        status.Text = "Расчёт до завершения";
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        if (!running) return;

        int count = finishMode ? 20 : 1;
        for (int i = 0; i < count && activeCells > 0; i++)
            Step();

        RenderField();
        UpdateStats();

        if (activeCells == 0)
        {
            Pause();
            status.Text = "Расчёт завершён";
        }
    }

    private void Step()
    {
        int[,] delta = new int[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int stones = field[y, x];
                int give = stones / 4;
                if (give == 0) continue;

                field[y, x] -= give * 4;

                if (y > 0) delta[y - 1, x] += give;
else lost += give;

                if (y < height - 1) delta[y + 1, x] += give;
                else lost += give;

                if (x > 0) delta[y, x - 1] += give;
                else lost += give;

                if (x < width - 1) delta[y, x + 1] += give;
                else lost += give;
            }
        }

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                field[y, x] += delta[y, x];

        totalSteps++;
        CountActive();

        if (history.Count >= 500) history.RemoveAt(0);
        history.Add(activeCells);
    }

    private void CountActive()
    {
        activeCells = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (field[y, x] >= 4) activeCells++;
    }

    private void RenderField()
    {
        int scale = Math.Max(1, (int)Math.Ceiling(Math.Max(width, height) / 1000.0));
        int imageWidth = (width + scale - 1) / scale;
        int imageHeight = (height + scale - 1) / scale;

        var bitmap = new Bitmap(imageWidth, imageHeight);
        Color[] colors =
        {
            Color.Black,
            Color.FromArgb(184, 115, 51),
            Color.LimeGreen,
            Color.White
        };

        for (int py = 0; py < imageHeight; py++)
        {
            for (int px = 0; px < imageWidth; px++)
            {
                int value = field[Math.Min(py * scale, height - 1), Math.Min(px * scale, width - 1)];
                bitmap.SetPixel(px, py, colors[value % 4]);
            }
        }

        var old = currentBitmap;
        currentBitmap = bitmap;
        picture.Image = bitmap;
        old?.Dispose();
    }

    private void UpdateStats()
    {
        long total = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                total += field[y, x];

        stats.Text =
            $"Размер: {width} × {height}\n\n" +
            $"Шагов: {totalSteps}\n\n" +
            $"Активных клеток: {activeCells}\n\n" +
            $"Камушков на поле: {total:N0}\n\n" +
            $"Потеряно за границей: {lost:N0}\n\n" +
            $"Состояние: {(running ? "Работает" : "Остановлено")}";

        graph.Invalidate();
    }

    private void DrawGraph(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(20, 20, 24));

        using var pen = new Pen(Color.LimeGreen, 2);
        using var gridPen = new Pen(Color.FromArgb(55, 55, 60));

        for (int i = 1; i < 5; i++)
        {
            int y = i * graph.Height / 5;
            g.DrawLine(gridPen, 0, y, graph.Width, y);
        }

        if (history.Count < 2) return;

        int max = Math.Max(1, history.Max());
        PointF[] points = new PointF[history.Count];

        for (int i = 0; i < history.Count; i++)
        {
            float x = i * (graph.Width - 1f) / Math.Max(1, history.Count - 1);
            float y = graph.Height - 5 - history[i] * (graph.Height - 10f) / max;
            points[i] = new PointF(x, y);
        }

        g.DrawLines(pen, points);
    }

    private void SaveImage()
    {
        if (currentBitmap == null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG image|*.png",
            FileName = "Sandpile.png"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            currentBitmap.Save(dialog.FileName, ImageFormat.Png);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        currentBitmap?.Dispose();
        base.OnFormClosed(e);
    }
}
