namespace HealthReminder.UI;

/// <summary>
/// 讓控制項在滑鼠進出時平滑過渡背景色，取代生硬的瞬間換色。
/// </summary>
public static class HoverGlow
{
    public static void Attach(
        Control control,
        Color normal,
        Color hover,
        Func<bool>? animationsEnabled = null)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        var amount = 0f;
        var direction = 0;

        void MoveTo(int nextDirection)
        {
            direction = nextDirection;
            if (animationsEnabled?.Invoke() == false)
            {
                timer.Stop();
                amount = direction > 0 ? 1f : 0f;
                control.BackColor = amount > 0 ? hover : normal;
                return;
            }

            timer.Start();
        }

        control.MouseEnter += (_, _) => MoveTo(1);
        control.MouseLeave += (_, _) => MoveTo(-1);
        timer.Tick += (_, _) =>
        {
            if (animationsEnabled?.Invoke() == false)
            {
                timer.Stop();
                amount = direction > 0 ? 1f : 0f;
                control.BackColor = amount > 0 ? hover : normal;
                return;
            }

            amount += direction * 0.2f;
            if (amount <= 0f) { amount = 0f; timer.Stop(); }
            else if (amount >= 1f) { amount = 1f; timer.Stop(); }
            control.BackColor = Lerp(normal, hover, amount);
        };
        control.Disposed += (_, _) => timer.Dispose();
    }

    private static Color Lerp(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(from.R + ((to.R - from.R) * amount)),
            (int)(from.G + ((to.G - from.G) * amount)),
            (int)(from.B + ((to.B - from.B) * amount)));
    }
}
