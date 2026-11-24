
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
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
    private const int MaxFieldSize = 10000;
    private const int MaxPreviewSize = 2000;
    private const int MaxGraphPoints = 5000;

    // Ключ словаря: y * fieldWidth + x.
    // Хранятся только клетки, в которых есть камни.
    private Dictionary<int, long> field = new();

    private int fieldWidth = 1000;
    private int fieldHeight = 1000;

    private int steps;
    private int activeCells;

    private long lostStones;
    private long initialStones = 1000;

    private readonly List<int> activity = new();

    // Цвета для 0, 1, 2 и 3+ камней.
    private readonly Color[] cellColors =
    {
        Color.Black,
        Color.FromArgb(184, 115, 51),
        Color.FromArgb(0, 180, 75),
        Color.White
    };

    private readonly SandpileView picture = new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.Black
    };

    private readonly Label status = new()
    {
        Text = "Готово",
        AutoSize = true,
        ForeColor = Color.LightGreen
    };

    private readonly Label stats = new()
    {
        AutoSize = true,
        ForeColor = Color.White
    };

    private readonly Panel graph = new()
    {
        Height = 180,
        Width = 230,
        BackColor = Color.FromArgb(35, 35, 42)
    };

    private readonly NumericUpDown widthBox =
        NumberBox(1000, 10, MaxFieldSize);

    private readonly NumericUpDown heightBox =
        NumberBox(1000, 10, MaxFieldSize);

    private readonly NumericUpDown stonesBox =
        NumberBox(1000, 1, long.MaxValue);

    private readonly System.Windows.Forms.Timer timer = new()
    {
        Interval = 30
    };

    public MainForm()
    {
        Text = "Sandpile Simulator";
        Width = 1450;
        Height = 850;
        MinimumSize = new Size(1050, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(28, 28, 34);
        ForeColor = Color.White;

        BuildInterface();

        timer.Tick += TimerTick;

        ResetField();
    }

    private static NumericUpDown NumberBox(
        long value,
        long min,
        long max)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = 195,
            ThousandsSeparator = true,
            BackColor = Color.FromArgb(55, 55, 62),
            ForeColor = Color.White
        };
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(28, 28, 34)
        };

        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 245));

        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));

        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 265));

        Controls.Add(root);

        // Левая панель

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(38, 38, 45)
        };

        root.Controls.Add(left, 0, 0);

        left.Controls.Add(Header("ПАРАМЕТРЫ ПОЛЯ"));

        left.Controls.Add(MakeLabel("Ширина поля:"));
        left.Controls.Add(widthBox);

        left.Controls.Add(MakeLabel("Высота поля:"));
        left.Controls.Add(heightBox);

        left.Controls.Add(MakeLabel("Количество камней:"));
        left.Controls.Add(stonesBox);

        left.Controls.Add(MakeButton(
            "Создать / Сбросить поле",
            (_, _) => ResetField()));

        left.Controls.Add(Header("УПРАВЛЕНИЕ"));

        left.Controls.Add(MakeButton(
            "▶ Запуск",
            (_, _) => Start()));

        left.Controls.Add(MakeButton(
            "Ⅱ Пауза",
            (_, _) => Pause()));

        left.Controls.Add(MakeButton(
            "Один шаг",
            (_, _) => OneStep()));

        left.Controls.Add(MakeButton(
            "До завершения",
            (_, _) => Finish()));

        left.Controls.Add(MakeButton(
            "Сохранить PNG",
            (_, _) => SaveImage()));

        left.Controls.Add(Header("ЦВЕТА КЛЕТОК"));

        left.Controls.Add(MakeButton(
            "Настроить цвета",
            (_, _) => ConfigureColors()));

        left.Controls.Add(Header("МАСШТАБ"));

        left.Controls.Add(MakeButton(
            "＋ Приблизить",
            (_, _) => picture.ZoomBy(1.25f)));

        left.Controls.Add(MakeButton(
            "－ Отдалить",
            (_, _) => picture.ZoomBy(0.8f)));

        left.Controls.Add(MakeButton(
            "Сброс масштаба",
            (_, _) => picture.ResetZoom()));

        left.Controls.Add(MakeButton(
            "Вместить всё поле",
            (_, _) => picture.FitImage()));

        left.Controls.Add(MakeLabel(
            "Колесо мыши — масштаб"));

        left.Controls.Add(MakeLabel(
            "Левая кнопка — перемещение"));

        left.Controls.Add(status);

        // Центральная панель

        root.Controls.Add(picture, 1, 0);

        // Правая панель

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
            AutoScroll = true,
            BackColor = Color.FromArgb(38, 38, 45)
        };

        root.Controls.Add(right, 2, 0);

        right.Controls.Add(Header("СТАТИСТИКА"));
        right.Controls.Add(stats);

        right.Controls.Add(Header("АКТИВНОСТЬ"));
        right.Controls.Add(graph);

        graph.Paint += DrawGraph;
    }

    private static Label Header(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 190, 90),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Margin = new Padding(0, 15, 0, 8)
        };
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.White,
            Margin = new Padding(0, 7, 0, 3)
        };
    }

    private static Button MakeButton(
        string text,
        EventHandler action)
    {
        var button = new Button
        {
            Text = text,
            Width = 205,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(65, 65, 75),
            ForeColor = Color.White,
            Margin = new Padding(0, 4, 0, 4)
        };

        button.FlatAppearance.BorderColor =
            Color.FromArgb(100, 100, 115);

        button.Click += action;

        return button;
    }

    // Выбор цветов

    private void ConfigureColors()
    {
        string[] names =
        {
            "0 камней",
            "1 камень",
            "2 камня",
            "3 и более камней"
        };

        for (int i = 0; i < cellColors.Length; i++)
        {
            using ColorDialog dialog = new ColorDialog
            {
                Color = cellColors[i],
                FullOpen = true,
                AnyColor = true
            };

            MessageBox.Show(
                this,
                $"Выберите цвет для состояния: {names[i]}",
                "Настройка цветов",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                cellColors[i] = dialog.Color;
            }
        }

        RenderField();
        status.Text = "Цвета обновлены";
    }

    // Создание поля

    private void ResetField()
    {
        Pause();

        fieldWidth = (int)widthBox.Value;
        fieldHeight = (int)heightBox.Value;

        initialStones = (long)stonesBox.Value;

        field = new Dictionary<int, long>();

        int centerX = fieldWidth / 2;
        int centerY = fieldHeight / 2;

        int centerKey = centerY * fieldWidth + centerX;

        field[centerKey] = initialStones;

        steps = 0;
        lostStones = 0;
        activity.Clear();

        activeCells = initialStones >= 4 ? 1 : 0;

        RenderField();
        UpdateStats();

        status.Text = "Поле создано";
    }

    private void Start()
    {
        if (activeCells == 0)
        {
            status.Text = "Модель уже завершена";
            return;
        }

        timer.Start();
        status.Text = "Моделирование...";
    }

    private void Pause()
    {
        timer.Stop();
        status.Text = "Пауза";
    }

    private void OneStep()
    {
        Pause();

        if (activeCells > 0)
        {
            Step();
            status.Text = "Выполнен один шаг";
        }
        else
        {
            status.Text = "Модель завершена";
        }
    }

    private void Finish()
    {
        Pause();

        int counter = 0;

        try
        {
            while (activeCells > 0)
            {
                Step(false);
                counter++;

                if (counter % 50 == 0)
                {
                    RenderField();
                    UpdateStats();
                    Application.DoEvents();
                }
            }

            RenderField();
            UpdateStats();

            status.Text = "Моделирование завершено";
        }
        catch (OutOfMemoryException)
        {
            status.Text = "Недостаточно оперативной памяти";

            MessageBox.Show(
                this,
                "Не хватает оперативной памяти для продолжения расчёта.",
                "Ошибка памяти",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        if (activeCells == 0)
        {
            Pause();
            status.Text = "Моделирование завершено";
            return;
        }

        try
        {
            Step();
        }
        catch (OutOfMemoryException)
        {
            Pause();

            MessageBox.Show(
                this,
                "Недостаточно оперативной памяти для продолжения расчёта.",
                "Ошибка памяти",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // Добавление камней в словарь

    private static void AddStones(
        Dictionary<int, long> target,
        int key,
        long amount)
    {
        if (amount <= 0)
            return;

        if (target.TryGetValue(key, out long current))
            target[key] = checked(current + amount);
        else
            target[key] = amount;
    }

    // Один параллельный шаг модели

    private void Step(bool redraw = true)
    {
        var next = new Dictionary<int, long>(
            Math.Max(field.Count, 16));

        long lostThisStep = 0;
        int nextActive = 0;

        foreach (var item in field)
        {
            int key = item.Key;
            long stones = item.Value;

            int x = key % fieldWidth;
            int y = key / fieldWidth;

            if (stones < 4)
            {
                AddStones(next, key, stones);
                continue;
            }

            long share = stones / 4;
            long remainder = stones % 4;

            AddStones(next, key, remainder);

            if (x > 0)
                AddStones(next, key - 1, share);
            else
                lostThisStep += share;

            if (x < fieldWidth - 1)
                AddStones(next, key + 1, share);
            else
                lostThisStep += share;

            if (y > 0)
                AddStones(next, key - fieldWidth, share);
            else
                lostThisStep += share;

            if (y < fieldHeight - 1)
                AddStones(next, key + fieldWidth, share);
            else
                lostThisStep += share;
        }

        foreach (long value in next.Values)
        {
            if (value >= 4)
                nextActive++;
        }

        field = next;

        steps++;
        lostStones += lostThisStep;

        activeCells = nextActive;

        activity.Add(activeCells);

        if (activity.Count > MaxGraphPoints)
            activity.RemoveAt(0);

        if (redraw)
        {
            RenderField();
            UpdateStats();
        }
    }

    // Отрисовка уменьшенного изображения

    private void RenderField()
    {
        int previewWidth = Math.Min(
            fieldWidth,
            MaxPreviewSize);

        int previewHeight = Math.Min(
            fieldHeight,
            MaxPreviewSize);

        using Bitmap bitmap = new Bitmap(
            previewWidth,
            previewHeight,
            PixelFormat.Format32bppArgb);

        int[] pixels = new int[previewWidth * previewHeight];

        // По умолчанию все клетки имеют цвет нулевого состояния.

        int zeroColor = cellColors[0].ToArgb();
        Array.Fill(pixels, zeroColor);

        // Если несколько клеток попадают в один пиксель,
        // показываем наиболее высокий уровень.

        byte[] levels = new byte[previewWidth * previewHeight];

        foreach (var item in field)
        {
            int key = item.Key;

            int x = key % fieldWidth;
            int y = key / fieldWidth;

            int px = x * previewWidth / fieldWidth;
            int py = y * previewHeight / fieldHeight;

            int index = py * previewWidth + px;

            byte level = item.Value switch
            {
                1 => 1,
                2 => 2,
                _ => 3
            };

            if (level > levels[index])
                levels[index] = level;
        }

        int[] colorValues =
        {
            cellColors[0].ToArgb(),
            cellColors[1].ToArgb(),
            cellColors[2].ToArgb(),
            cellColors[3].ToArgb()
        };

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = colorValues[levels[i]];
        }

        Rectangle rect = new Rectangle(
            0, 0, previewWidth, previewHeight);

        BitmapData data = bitmap.LockBits(
            rect,
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            for (int y = 0; y < previewHeight; y++)
            {
                IntPtr row = IntPtr.Add(
                    data.Scan0,
                    y * data.Stride);

                Marshal.Copy(
                    pixels,
                    y * previewWidth,
                    row,
                    previewWidth);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        picture.Image = new Bitmap(bitmap);
    }

    // Статистика

    private void UpdateStats()
    {
        long total = 0;

        foreach (long value in field.Values)
            total = checked(total + value);

        stats.Text =
            $"Размер поля: {fieldWidth} × {fieldHeight}\n\n" +
            $"Шагов: {steps:N0}\n\n" +
            $"Камней осталось: {total:N0}\n\n" +
            $"Потеряно за границами: {lostStones:N0}\n\n" +
            $"Активных клеток: {activeCells:N0}\n\n" +
            $"Начальное количество: {initialStones:N0}\n\n" +
            $"Занятых клеток: {field.Count:N0}";

        graph.Invalidate();
    }

    // График активности

    private void DrawGraph(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        g.Clear(Color.FromArgb(35, 35, 42));

        if (activity.Count < 2)
            return;

        int max = 1;

        foreach (int value in activity)
            max = Math.Max(max, value);

        int width = graph.ClientSize.Width - 20;
        int height = graph.ClientSize.Height - 30;

        if (width <= 0 || height <= 0)
            return;

        using Pen pen = new Pen(
            Color.FromArgb(255, 180, 70), 2);

        PointF[] points = new PointF[activity.Count];

        for (int i = 0; i < activity.Count; i++)
        {
            float x = 10 +
                (float)i / (activity.Count - 1) * width;

            float y = 10 +
                height -
                (float)activity[i] / max * height;

            points[i] = new PointF(x, y);
        }

        g.DrawLines(pen, points);
    }

    // Сохранение PNG

    private void SaveImage()
    {
        if (picture.Image == null)
            return;

        using SaveFileDialog dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = "Sandpile.png"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            picture.Image.Save(
                dialog.FileName,
                ImageFormat.Png);

            status.Text = "Изображение сохранено";
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        timer.Dispose();

        picture.Image = null;

        base.OnFormClosed(e);
    }
}

// Отображение поля, масштабирование и перемещение

public class SandpileView : Control
{
    private Image? image;

    private float zoom = 1f;
    private float offsetX;
    private float offsetY;

    private bool dragging;
    private bool fitMode = true;

    private Point lastMouse;

    public Image? Image
    {
        get => image;

        set
        {
            if (image != value)
                image?.Dispose();

            image = value;

            FitImage();
            Invalidate();
        }
    }

    public SandpileView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        BackColor = Color.Black;
    }

    private float FitScale()
    {
        if (image == null ||
            ClientSize.Width <= 0 ||
            ClientSize.Height <= 0)
            return 1f;

        return Math.Min(
            (float)ClientSize.Width / image.Width,
            (float)ClientSize.Height / image.Height);
    }

    public void FitImage()
    {
        zoom = 1f;
        fitMode = true;

        CenterImage();
        Invalidate();
    }

    public void ResetZoom()
    {
        FitImage();
    }

    private void CenterImage()
    {
        if (image == null)
            return;

        float scale = FitScale() * zoom;

        offsetX =
            (ClientSize.Width - image.Width * scale) / 2f;

        offsetY =
            (ClientSize.Height - image.Height * scale) / 2f;
    }

    public void ZoomBy(float factor)
    {
        ZoomAt(
            factor,
            new Point(
                ClientSize.Width / 2,
                ClientSize.Height / 2));
    }

    private void ZoomAt(float factor, Point cursor)
    {
        if (image == null)
            return;

        float oldScale = FitScale() * zoom;

        float newZoom = Math.Clamp(
            zoom * factor,
            0.1f,
            50f);

        float newScale = FitScale() * newZoom;

        if (oldScale <= 0 || newScale <= 0)
            return;

        float imageX = (cursor.X - offsetX) / oldScale;
        float imageY = (cursor.Y - offsetY) / oldScale;

        offsetX = cursor.X - imageX * newScale;
        offsetY = cursor.Y - imageY * newScale;

        zoom = newZoom;
        fitMode = false;

        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        Focus();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ZoomAt(
            e.Delta > 0 ? 1.2f : 0.8f,
            e.Location);

        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();

        if (e.Button == MouseButtons.Left)
        {
            dragging = true;
            lastMouse = e.Location;
            Cursor = Cursors.Hand;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging)
        {
            offsetX += e.X - lastMouse.X;
            offsetY += e.Y - lastMouse.Y;

            lastMouse = e.Location;

            fitMode = false;

            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            dragging = false;
            Cursor = Cursors.Default;
        }

        base.OnMouseUp(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        dragging = false;
        Cursor = Cursors.Default;

        base.OnMouseCaptureChanged(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (fitMode)
            CenterImage();

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(Color.Black);

        if (image == null)
            return;

        float scale = FitScale() * zoom;

        e.Graphics.InterpolationMode =
            InterpolationMode.NearestNeighbor;

        e.Graphics.PixelOffsetMode =
            PixelOffsetMode.Half;

        e.Graphics.DrawImage(
            image,
            offsetX,
            offsetY,
            image.Width * scale,
            image.Height * scale);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            image?.Dispose();
            image = null;
        }

        base.Dispose(disposing);
    }
}

