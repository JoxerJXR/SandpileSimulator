
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

public class SourcePoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public long Stones { get; set; }

    public SourcePoint(int x, int y, long stones)
    {
        X = x;
        Y = y;
        Stones = stones;
    }
}

public class MainForm : Form
{
    private const int MaxFieldSize = 10000;
    private const int MaxGraphPoints = 5000;
    private const int MaxSourcePoints = 10;

    private Dictionary<int, long> field = new();

    private readonly List<SourcePoint> sourcePoints = new();

    private int fieldWidth = 1000;
    private int fieldHeight = 1000;

    private int steps;
    private int activeCells;

    private long lostStones;
    private long initialStones;

    private readonly List<int> activity = new();

    private readonly Color[] cellColors =
    {
        Color.Black,
        Color.FromArgb(184, 115, 51),
        Color.FromArgb(0, 180, 75),
        Color.White
    };

    private readonly Color[] pointColors =
    {
        Color.Red,
        Color.DeepSkyBlue,
        Color.Yellow,
        Color.Magenta,
        Color.Lime,
        Color.Orange,
        Color.Cyan,
        Color.HotPink,
        Color.Gold,
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
        ForeColor = Color.LightGreen,
        MaximumSize = new Size(215, 0)
    };

    private readonly Label cellInfo = new()
    {
        Text = "Координаты: —",
        AutoSize = true,
        ForeColor = Color.LightSkyBlue,
        MaximumSize = new Size(215, 0)
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

    private readonly ListBox pointList = new()
    {
        Width = 205,
        Height = 125,
        BackColor = Color.FromArgb(55, 55, 62),
        ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle
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
        Width = 1500;
        Height = 900;
        MinimumSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(28, 28, 34);
        ForeColor = Color.White;

        BuildInterface();

        timer.Tick += TimerTick;

        picture.CellClicked += AddSourcePoint;
        picture.PointMoved += MoveSourcePoint;
        picture.CellHovered += ShowCellInfo;

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

        left.Controls.Add(MakeLabel("Камней для новой точки:"));
        left.Controls.Add(stonesBox);

        left.Controls.Add(MakeButton(
            "Создать / Сбросить поле",
            (_, _) => ResetField()));

        left.Controls.Add(Header("ТОЧКИ ИСТОЧНИКОВ"));

        left.Controls.Add(MakeLabel(
            "Выбери количество камней,\nзатем режим добавления и\nщелкни по нужной клетке."));

        left.Controls.Add(MakeButton(
            "＋ Добавить точку",
            (_, _) => SetViewMode(ViewMode.AddPoint)));

        left.Controls.Add(MakeButton(
            "Переместить точку",
            (_, _) => SetViewMode(ViewMode.MovePoint)));

        left.Controls.Add(MakeButton(
            "Режим перемещения поля",
            (_, _) => SetViewMode(ViewMode.Pan)));

        left.Controls.Add(MakeLabel("Список точек:"));
        left.Controls.Add(pointList);

        left.Controls.Add(MakeButton(
            "Удалить выбранную точку",
            (_, _) => RemoveSelectedPoint()));

        left.Controls.Add(MakeButton(
            "Удалить все точки",
            (_, _) => ClearAllPoints()));

        left.Controls.Add(MakeLabel(
            "Максимум точек: 10"));

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
            "В режиме перемещения:\nлевая кнопка — перетаскивание"));

        left.Controls.Add(cellInfo);
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

    private void SetViewMode(ViewMode mode)
    {
        picture.Mode = mode;

        switch (mode)
        {
            case ViewMode.AddPoint:
                status.Text = "Щёлкни по полю, чтобы добавить точку";
                break;

            case ViewMode.MovePoint:
                status.Text = "Зажми точку и перетащи её";
                break;

            default:
                status.Text = "Режим перемещения поля";
                break;
        }
    }

    // Создание поля

    private void ResetField()
    {
        Pause();

        fieldWidth = (int)widthBox.Value;
        fieldHeight = (int)heightBox.Value;

        sourcePoints.Clear();

        int centerX = fieldWidth / 2;
        int centerY = fieldHeight / 2;

        sourcePoints.Add(new SourcePoint(
            centerX,
            centerY,
            (long)stonesBox.Value));

        steps = 0;
        lostStones = 0;
        activity.Clear();

        RebuildFieldFromPoints();

        picture.FitImage();

        status.Text = "Поле создано. Начальная точка в центре";
    }

    private void RebuildFieldFromPoints()
    {
        field = new Dictionary<int, long>();

        initialStones = 0;

        foreach (SourcePoint point in sourcePoints)
        {
            int key = point.Y * fieldWidth + point.X;

            AddStones(field, key, point.Stones);

            initialStones = checked(
                initialStones + point.Stones);
        }

        activeCells = field.Values.Count(v => v >= 4);

        RefreshPointList();
        RenderField();
        UpdateStats();
    }

    // Добавление точки

    private void AddSourcePoint(int x, int y)
    {
        if (steps > 0)
        {
            status.Text = "Сначала сбрось поле, чтобы изменить точки";
            return;
        }

        if (sourcePoints.Count >= MaxSourcePoints)
        {
            status.Text = "Достигнут предел: 10 точек";
            return;
        }

        long stones = (long)stonesBox.Value;

        try
        {
            checked
            {
                long total = initialStones + stones;
            }
        }
        catch (OverflowException)
        {
            MessageBox.Show(
                this,
                "Общее количество камней не может превышать Long.MaxValue.",
                "Слишком много камней",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        sourcePoints.Add(new SourcePoint(x, y, stones));

        RebuildFieldFromPoints();

        status.Text =
            $"Добавлена точка №{sourcePoints.Count}: X={x}, Y={y}";
    }

    // Перемещение точки

    private void MoveSourcePoint(int index, int x, int y)
    {
        if (steps > 0 || index < 0 || index >= sourcePoints.Count)
            return;

        SourcePoint point = sourcePoints[index];

        if (point.X == x && point.Y == y)
            return;

        point.X = x;
        point.Y = y;

        RebuildFieldFromPoints();

        status.Text =
            $"Точка №{index + 1}: X={x}, Y={y}";
    }

    // Удаление выбранной точки

    private void RemoveSelectedPoint()
    {
        if (steps > 0)
        {
            status.Text = "Сначала сбрось поле";
            return;
        }

        int index = pointList.SelectedIndex;

        if (index < 0 || index >= sourcePoints.Count)
        {
            status.Text = "Сначала выбери точку в списке";
            return;
        }

        sourcePoints.RemoveAt(index);

        RebuildFieldFromPoints();

        status.Text = "Точка удалена";
    }

    private void ClearAllPoints()
    {
        if (steps > 0)
        {
            status.Text = "Сначала сбрось поле";
            return;
        }

        sourcePoints.Clear();

        RebuildFieldFromPoints();

        status.Text = "Все точки удалены";
    }

    private void RefreshPointList()
    {
        int selected = pointList.SelectedIndex;

        pointList.Items.Clear();

        for (int i = 0; i < sourcePoints.Count; i++)
        {
            SourcePoint point = sourcePoints[i];

            pointList.Items.Add(
                $"№{i + 1}  X:{point.X}  Y:{point.Y}  Камней:{point.Stones:N0}");
        }

        if (pointList.Items.Count > 0)
        {
            pointList.SelectedIndex = Math.Clamp(
                selected, 0, pointList.Items.Count - 1);
        }

        picture.SetData(
            field,
            fieldWidth,
            fieldHeight,
            cellColors,
            sourcePoints,
            pointColors);
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
                cellColors[i] = dialog.Color;
        }

        RenderField();
        status.Text = "Цвета обновлены";
    }

    // Управление моделью

    private void Start()
    {
        if (activeCells == 0)
        {
            status.Text = "Модель уже завершена";
            return;
        }

        picture.Mode = ViewMode.Pan;

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

    // Добавление камней

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

    // Один параллельный шаг

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
                lostThisStep = checked(lostThisStep + share);

            if (x < fieldWidth - 1)
                AddStones(next, key + 1, share);
            else
                lostThisStep = checked(lostThisStep + share);

            if (y > 0)
                AddStones(next, key - fieldWidth, share);
            else
                lostThisStep = checked(lostThisStep + share);

            if (y < fieldHeight - 1)
                AddStones(next, key + fieldWidth, share);
            else
                lostThisStep = checked(lostThisStep + share);
        }

        foreach (long value in next.Values)
        {
            if (value >= 4)
                nextActive++;
        }

        field = next;

        steps++;
        lostStones = checked(lostStones + lostThisStep);

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

    // Обновление отображения

    private void RenderField()
    {
        picture.SetData(
            field,
            fieldWidth,
            fieldHeight,
            cellColors,
            sourcePoints,
            pointColors);

        picture.Invalidate();
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
            $"Точек источников: {sourcePoints.Count}\n\n" +
            $"Занятых клеток: {field.Count:N0}";

        graph.Invalidate();
    }

    private void ShowCellInfo(int x, int y, long stones)
    {
        cellInfo.Text =
            $"Координаты: X={x}, Y={y}\nКамней в клетке: {stones:N0}";
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
        using SaveFileDialog dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = "Sandpile.png"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            int previewWidth = Math.Min(fieldWidth, 2000);
            int previewHeight = Math.Min(fieldHeight, 2000);

            int[] pixels = new int[previewWidth * previewHeight];

            byte[] levels = new byte[pixels.Length];

            foreach (var item in field)
            {
                int x = item.Key % fieldWidth;
                int y = item.Key / fieldWidth;

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

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = cellColors[levels[i]].ToArgb();

            using Bitmap bitmap = new Bitmap(
                previewWidth,
                previewHeight,
                PixelFormat.Format32bppArgb);

            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, previewWidth, previewHeight),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                for (int y = 0; y < previewHeight; y++)
                {
                    Marshal.Copy(
                        pixels,
                        y * previewWidth,
                        IntPtr.Add(data.Scan0, y * data.Stride),
                        previewWidth);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            using Graphics g = Graphics.FromImage(bitmap);

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                SourcePoint point = sourcePoints[i];

                float x = (float)point.X / fieldWidth * previewWidth;
                float y = (float)point.Y / fieldHeight * previewHeight;

                using Brush brush = new SolidBrush(pointColors[i]);

                g.FillEllipse(brush, x - 5, y - 5, 10, 10);
            }

            bitmap.Save(dialog.FileName, ImageFormat.Png);

            status.Text = "PNG сохранён";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Не удалось сохранить изображение:\n" + ex.Message,
                "Ошибка сохранения",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        timer.Dispose();

        base.OnFormClosed(e);
    }
}

public enum ViewMode
{
    Pan,
    AddPoint,
    MovePoint
}

// Отрисовка исходного поля, а не растянутой картинки

public class SandpileView : Control
{
    private Dictionary<int, long> field = new();

    private int fieldWidth = 1000;
    private int fieldHeight = 1000;

    private Color[] colors =
    {
        Color.Black,
        Color.Brown,
        Color.Green,
        Color.White
    };

    private List<SourcePoint> points = new();

    private Color[] pointColors = Array.Empty<Color>();

    private float zoom = 1f;
    private float offsetX;
    private float offsetY;

    private bool fitMode = true;
    private bool dragging;

    private int draggingPoint = -1;
    private int lastPointX = -1;
    private int lastPointY = -1;

    private Point lastMouse;

    public ViewMode Mode { get; set; } = ViewMode.Pan;

    public event Action<int, int>? CellClicked;
    public event Action<int, int, int>? PointMoved;
    public event Action<int, int, long>? CellHovered;

    public SandpileView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        BackColor = Color.Black;
    }

    public void SetData(
        Dictionary<int, long> newField,
        int width,
        int height,
        Color[] newColors,
        List<SourcePoint> newPoints,
        Color[] newPointColors)
    {
        field = newField;
        fieldWidth = width;
        fieldHeight = height;
        colors = newColors;
        points = newPoints;
        pointColors = newPointColors;

        Invalidate();
    }

    private float FitScale()
    {
        if (ClientSize.Width <= 0 ||
            ClientSize.Height <= 0 ||
            fieldWidth <= 0 ||
            fieldHeight <= 0)
            return 1f;

        return Math.Min(
            (float)ClientSize.Width / fieldWidth,
            (float)ClientSize.Height / fieldHeight);
    }

    private float CurrentScale()
    {
        return FitScale() * zoom;
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
        float scale = CurrentScale();

        offsetX =
            (ClientSize.Width - fieldWidth * scale) / 2f;

        offsetY =
            (ClientSize.Height - fieldHeight * scale) / 2f;
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
        float oldScale = CurrentScale();

        float newZoom = Math.Clamp(
            zoom * factor,
            0.1f,
            2000f);

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

    private bool TryGetCell(Point mouse, out int x, out int y)
    {
        float scale = CurrentScale();

        x = -1;
        y = -1;

        if (scale <= 0)
            return false;

        x = (int)Math.Floor((mouse.X - offsetX) / scale);
        y = (int)Math.Floor((mouse.Y - offsetY) / scale);

        return x >= 0 && x < fieldWidth &&
               y >= 0 && y < fieldHeight;
    }

    private int FindPoint(Point mouse)
    {
        float scale = CurrentScale();

        for (int i = points.Count - 1; i >= 0; i--)
        {
            SourcePoint point = points[i];

            float px = offsetX + (point.X + 0.5f) * scale;
            float py = offsetY + (point.Y + 0.5f) * scale;

            float dx = mouse.X - px;
            float dy = mouse.Y - py;

            if (dx * dx + dy * dy <= 18 * 18)
                return i;
        }

        return -1;
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

        if (e.Button != MouseButtons.Left)
        {
            base.OnMouseDown(e);
            return;
        }

        if (Mode == ViewMode.AddPoint)
        {
            if (TryGetCell(e.Location, out int x, out int y))
                CellClicked?.Invoke(x, y);

            base.OnMouseDown(e);
            return;
        }

        if (Mode == ViewMode.MovePoint)
        {
            int index = FindPoint(e.Location);

            if (index >= 0)
            {
                draggingPoint = index;
                dragging = true;

                if (TryGetCell(e.Location, out int x, out int y))
                {
                    lastPointX = x;
                    lastPointY = y;
                }

                Cursor = Cursors.SizeAll;
            }

            base.OnMouseDown(e);
            return;
        }

        dragging = true;
        lastMouse = e.Location;
        Cursor = Cursors.Hand;

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging && draggingPoint >= 0)
        {
            if (TryGetCell(e.Location, out int x, out int y))
            {
                if (x != lastPointX || y != lastPointY)
                {
                    PointMoved?.Invoke(draggingPoint, x, y);

                    lastPointX = x;
                    lastPointY = y;
                }
            }
        }
        else if (dragging && Mode == ViewMode.Pan)
        {
            offsetX += e.X - lastMouse.X;
            offsetY += e.Y - lastMouse.Y;

            lastMouse = e.Location;

            fitMode = false;

            Invalidate();
        }
        else if (TryGetCell(e.Location, out int x, out int y))
        {
            int key = y * fieldWidth + x;

            field.TryGetValue(key, out long stones);

            CellHovered?.Invoke(x, y, stones);
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            dragging = false;
            draggingPoint = -1;
            Cursor = Cursors.Default;
        }

        base.OnMouseUp(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        dragging = false;
        draggingPoint = -1;
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

        Graphics g = e.Graphics;

        g.Clear(colors[0]);

        float scale = CurrentScale();

        if (scale <= 0)
            return;

        g.SmoothingMode = SmoothingMode.None;

        int left = Math.Clamp(
            (int)Math.Floor((0 - offsetX) / scale),
            0,
            fieldWidth);

        int top = Math.Clamp(
            (int)Math.Floor((0 - offsetY) / scale),
            0,
            fieldHeight);

        int right = Math.Clamp(
            (int)Math.Ceiling((ClientSize.Width - offsetX) / scale),
            0,
            fieldWidth);

        int bottom = Math.Clamp(
            (int)Math.Ceiling((ClientSize.Height - offsetY) / scale),
            0,
            fieldHeight);

        if (scale >= 1f)
        {
            DrawIndividualCells(g, scale, left, top, right, bottom);
        }
        else
        {
            DrawReducedCells(g, scale);
        }

        // Сетка пустых и заполненных клеток

        if (scale >= 6f)
            DrawGrid(g, scale, left, top, right, bottom);

        DrawSourcePoints(g, scale);
    }

    // Приближение: каждая исходная клетка отдельно

    private void DrawIndividualCells(
        Graphics g,
        float scale,
        int left,
        int top,
        int right,
        int bottom)
    {
        using SolidBrush brush1 = new SolidBrush(colors[1]);
        using SolidBrush brush2 = new SolidBrush(colors[2]);
        using SolidBrush brush3 = new SolidBrush(colors[3]);

        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                int key = y * fieldWidth + x;

                if (!field.TryGetValue(key, out long stones))
                    continue;

                Brush brush = stones switch
                {
                    1 => brush1,
                    2 => brush2,
                    _ => brush3
                };

                float px = offsetX + x * scale;
                float py = offsetY + y * scale;

                g.FillRectangle(
                    brush,
                    px,
                    py,
                    scale + 0.5f,
                    scale + 0.5f);
            }
        }
    }

    // Отдаление: объединение клеток в экранные пиксели

    private void DrawReducedCells(Graphics g, float scale)
    {
        int screenWidth = ClientSize.Width;

        if (screenWidth <= 0)
            return;

        var pixels = new Dictionary<int, byte>();

        foreach (var item in field)
        {
            int x = item.Key % fieldWidth;
            int y = item.Key / fieldWidth;

            int px = (int)Math.Floor(offsetX + (x + 0.5f) * scale);
            int py = (int)Math.Floor(offsetY + (y + 0.5f) * scale);

            if (px < 0 || px >= ClientSize.Width ||
                py < 0 || py >= ClientSize.Height)
                continue;

            int index = py * screenWidth + px;

            byte level = item.Value switch
            {
                1 => 1,
                2 => 2,
                _ => 3
            };

            if (!pixels.TryGetValue(index, out byte old) ||
                level > old)
            {
                pixels[index] = level;
            }
        }

        using SolidBrush brush1 = new SolidBrush(colors[1]);
        using SolidBrush brush2 = new SolidBrush(colors[2]);
        using SolidBrush brush3 = new SolidBrush(colors[3]);

        foreach (var item in pixels)
        {
            int px = item.Key % screenWidth;
            int py = item.Key / screenWidth;

            Brush brush = item.Value switch
            {
                1 => brush1,
                2 => brush2,
                _ => brush3
            };

            g.FillRectangle(brush, px, py, 1, 1);
        }
    }

    // Сетка клеток

    private void DrawGrid(
        Graphics g,
        float scale,
        int left,
        int top,
        int right,
        int bottom)
    {
        using Pen gridPen = new Pen(
            Color.FromArgb(65, 180, 180, 180), 1f);

        for (int x = left; x <= right; x++)
        {
            float px = offsetX + x * scale;

            g.DrawLine(
                gridPen,
                px,
                offsetY + top * scale,
                px,
                offsetY + bottom * scale);
        }

        for (int y = top; y <= bottom; y++)
        {
            float py = offsetY + y * scale;

            g.DrawLine(
                gridPen,
                offsetX + left * scale,
                py,
                offsetX + right * scale,
                py);
        }
    }

    // Маркеры точек источников

    private void DrawSourcePoints(Graphics g, float scale)
    {
        for (int i = 0; i < points.Count; i++)
        {
            SourcePoint point = points[i];

            float x = offsetX + (point.X + 0.5f) * scale;
            float y = offsetY + (point.Y + 0.5f) * scale;

            if (x < -30 || x > ClientSize.Width + 30 ||
                y < -30 || y > ClientSize.Height + 30)
                continue;

            float radius = Math.Clamp(scale * 0.4f, 5f, 12f);

            using SolidBrush brush =
                new SolidBrush(pointColors[i % pointColors.Length]);

            using Pen outline = new Pen(Color.Black, 2f);

            g.FillEllipse(
                brush,
                x - radius,
                y - radius,
                radius * 2,
                radius * 2);

            g.DrawEllipse(
                outline,
                x - radius,
                y - radius,
                radius * 2,
                radius * 2);

            using Font font = new Font(
                "Segoe UI",
                9,
                FontStyle.Bold);

            using SolidBrush textBrush =
                new SolidBrush(Color.White);

            g.DrawString(
                (i + 1).ToString(),
                font,
                textBrush,
                x + radius,
                y - radius);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}

