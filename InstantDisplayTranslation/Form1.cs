using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FLib;

namespace InstantDisplayTranslation
{
    public partial class Form1 : Form
    {
        Hooker hooker = new Hooker();
        Rectangle[] virtualScreenBounds = null;
        Pen pen = new Pen(Brushes.Black);
        Font font = new Font("Arial", 64);
        bool enable = false;
        Point prevCursorPosition = Point.Empty;
        ShortcutKey shortcutKey = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            prevCursorPosition = Cursor.Position;
            RefleshVirtualScreenBounds();
            hooker.OnKeyHook = OnKeyHook;
            hooker.OnMouseHook = OnMouseHook;
            hooker.Hook();

            for (int i = 'A'; i <= 'Z'; i++)
                keyCombobox.Items.Add("" + (char)i);
            for (int i = 1; i <= 24; i++)
                keyCombobox.Items.Add("F" + i);

            shortcutKey = GetShortcutKey();

            enable = enableCheckBox.Checked;
        }

        bool OnKeyHook(int code, WM wParam, KBDLLHOOKSTRUCT lParam, Hooker hooker)
        {
            // F1キー
            if (OnShortcutkey(lParam.vkCode))
            {
                var bounds = GetVirtualSecondaryScreenBounds(Cursor.Position);
                if (!bounds.IsEmpty && virtualScreenBounds.Length >= 2)
                    virtualScreenBounds[1] = bounds;
                this.TopMost = true;
                this.TopMost = false;
                canvas.Invalidate();
                return true;
            }
            return false;
        }

        bool OnShortcutkey(int vkCode)
        {
            if (shortcutKey == null)
                return false;

            if (shortcutKey.ctrl && !hooker.onCtrl)
                return false;

            if (shortcutKey.alt && !hooker.onAlt)
                return false;

            if (shortcutKey.shift && !hooker.onShift)
                return false;

            if (shortcutKey.key == "")
                return false;

            if (shortcutKey.key.Length == 1)
            {
                if (char.ToUpper(shortcutKey.key[0]) != vkCode && char.ToLower(shortcutKey.key[0]) != vkCode)
                    return false;
            }
            else
            {
                if (shortcutKey.key[0] != 'F')
                    return false;
                int idx;
                if (!int.TryParse(shortcutKey.key.Substring(1), out idx))
                    return false;
                if (idx < 1 && 24 < idx)
                    return false;
                if (0x70 + idx - 1 != vkCode)
                    return false;
            }

            return true;
        }

        bool OnMouseHook(int code, WM message, IntPtr state, Hooker hooker)
        {
            if (enable)
            {
                if (message == WM.MOUSEMOVE)
                {
                    Point curPt = Cursor.Position;
                    Point newPt = ToVirtualWorld(curPt, prevCursorPosition);
                    Cursor.Position = newPt;
                    prevCursorPosition = newPt;
                    if (curPt != newPt)
                        return true;
                }
            }
            return false;
        }

        int GetBoundsIdx(Rectangle[] bounds, Point pt)
        {
            for (int i = 0; i < bounds.Length; i++)
                if (bounds[i].Contains(pt))
                    return i;
            return -1;
        }

        private Point ToVirtualWorld(Point pt, Point prevPt)
        {
            var rbounds = GetRealScreenBounds();
            var vbounds = GetVirtualScreenBounds();
            if (rbounds == null || vbounds == null || vbounds.Length <= 1 || vbounds.Length != rbounds.Length)
                return pt;

            int prev = GetBoundsIdx(rbounds, prevPt);
            if (prev <= -1)
                return pt;

            int cur = GetBoundsIdx(rbounds, pt);

            // 勝手に他のスクリーンに移動してはいけない
            if (cur != prev)
            {
                pt.X = Math.Min(Math.Max(pt.X, rbounds[prev].Left), rbounds[prev].Right - 1);
                pt.Y = Math.Min(Math.Max(pt.Y, rbounds[prev].Top), rbounds[prev].Bottom - 1);
                cur = prev;
            }

            if (cur <= -1)
                return pt;
            Point vPt = new Point(pt.X - rbounds[cur].X + vbounds[cur].X, pt.Y - rbounds[cur].Y + vbounds[cur].Y);

            Point nextPoint = vPt;
            int next = -1;
            for (int i = 0; i < vbounds.Length; i++)
            {
                if (i == cur)
                    continue;

                if (vbounds[i].Contains(new Point(vPt.X - 1, vPt.Y)))
                    nextPoint = new Point(vPt.X - 3, vPt.Y);

                if (vbounds[i].Contains(new Point(vPt.X + 1, vPt.Y)))
                    nextPoint = new Point(vPt.X + 3, vPt.Y);

                if (vbounds[i].Contains(new Point(vPt.X, vPt.Y - 1)))
                    nextPoint = new Point(vPt.X, vPt.Y - 3);

                if (vbounds[i].Contains(new Point(vPt.X, vPt.Y + 1)))
                    nextPoint = new Point(vPt.X, vPt.Y + 3);

                if (nextPoint != vPt)
                {
                    next = i;
                    break;
                }
            }

            if (next <= -1)
                return pt;

            Point newPt = new Point(nextPoint.X - vbounds[next].X + rbounds[next].X, nextPoint.Y - vbounds[next].Y + rbounds[next].Y);

            return newPt;
        }

        Rectangle GetVirtualSecondaryScreenBounds(Point pt)
        {
            const int cornerSize = 50;

            if (Screen.AllScreens.Length <= 1)
                return Rectangle.Empty;

            var scr1 = Screen.PrimaryScreen.Bounds;
            int w1 = scr1.Width;
            int h1 = scr1.Height;

            var scr2 = Screen.AllScreens[1].Bounds;
            int w2 = scr2.Width;
            int h2 = scr2.Height;

            if (Screen.PrimaryScreen.Bounds.Contains(pt))
            {
                Point pt1 = pt;
                pt1.X -= scr1.X;
                pt1.Y -= scr1.Y;

                if (pt1.X <= cornerSize && pt1.Y <= cornerSize) // 左上
                    return new Rectangle(scr1.X - w2, scr1.Y - h2 * 2 / 3, w2, h2);
                else if (pt1.X >= w1 - cornerSize && pt1.Y <= cornerSize) // 右上
                    return new Rectangle(scr1.X + w1, scr1.Y - h2 * 2 / 3, w2, h2);
                else if (pt1.X <= cornerSize && pt1.Y >= h1 - cornerSize) // 左下
                    return new Rectangle(scr1.X - w2, scr1.Y + h1 - h2 / 3, w2, h2);
                else if (pt1.X >= w1 - cornerSize && pt1.Y >= h1 - cornerSize) // 右下
                    return new Rectangle(scr1.X + w1, scr1.Y + h1 - h2 / 3, w2, h2);
                else if (pt1.X < cornerSize) // 左
                    return new Rectangle(scr1.X - w2, scr1.Y, w2, h2);
                else if (pt1.Y < cornerSize) // 上
                    return new Rectangle(scr1.X, scr1.Y - h2, w2, h2);
                else if (pt1.X >= w1 - cornerSize) // 右
                    return new Rectangle(scr1.X + w1, scr1.Y, w2, h2);
                else if (pt1.Y >= h1 - cornerSize) // 下
                    return new Rectangle(scr1.X, scr1.Y + h1, w2, h2);
            }

            if (Screen.AllScreens[1].Bounds.Contains(pt))
            {
                Point pt2 = pt;
                pt2.X -= scr2.X;
                pt2.Y -= scr2.Y;

                if (pt2.X <= cornerSize && pt2.Y <= cornerSize) // 左上
                    return new Rectangle(scr1.X + w1, scr1.Y + h1 - h2 / 3, w2, h2);
                else if (pt2.X >= w2 - cornerSize && pt2.Y <= cornerSize) // 右上
                    return new Rectangle(scr1.X - w2, scr1.Y + h1 - h2 / 3, w2, h2);
                else if (pt2.X <= cornerSize && pt2.Y >= h1 - cornerSize) // 左下
                    return new Rectangle(scr1.X + w1, scr1.Y - h2 * 2 / 3, w2, h2);
                else if (pt2.X >= w2 - cornerSize && pt2.Y >= h1 - cornerSize) // 右下
                    return new Rectangle(scr1.X - w2, scr1.Y - h2 * 2 / 3, w2, h2);
                else if (pt2.X < cornerSize) // 左
                    return new Rectangle(scr1.X + w1, scr1.Y, w2, h2);
                else if (pt2.Y < cornerSize) // 上
                    return new Rectangle(scr1.X, scr1.Y + h1, w2, h2);
                else if (pt2.X >= w2 - cornerSize) // 右
                    return new Rectangle(scr1.X - w2, scr1.Y, w2, h2);
                else if (pt2.Y >= h1 - cornerSize) // 下
                    return new Rectangle(scr1.X, scr1.Y - h2, w2, h2);
            }

            return Rectangle.Empty;
        }

        private void enableCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            enable = enableCheckBox.Checked;
        }

        private void canvas_Paint(object sender, PaintEventArgs e)
        {
            const float scale = 0.1f;

            e.Graphics.Clear(Color.White);

            var bounds = GetScreenBounds();
            if (bounds == null || bounds.Length <= 0)
                return;

            float cx = bounds[0].X + bounds[0].Width / 2;
            float cy = bounds[0].Y + bounds[0].Height / 2;
            cx *= scale;
            cy *= scale;

            float ox = e.Graphics.ClipBounds.Width / 2 - cx;
            float oy = e.Graphics.ClipBounds.Height / 2 - cy;

            for (int i = 0; i < bounds.Length; i++)
            {
                float x = bounds[i].X * scale + ox;
                float y = bounds[i].Y * scale + oy;
                float w = bounds[i].Width * scale;
                float h = bounds[i].Height* scale;
                e.Graphics.FillRectangle(Brushes.Beige, x, y, w, h);
                e.Graphics.DrawRectangle(pen, x, y, w, h);

                string text = "" + (i + 1);
                var size = e.Graphics.MeasureString(text, font);
                float tx = x + w / 2 - size.Width / 2;
                float ty = y + h / 2 - size.Height / 2;
                e.Graphics.DrawString(text, font, Brushes.Black, tx, ty);
            }
        }

        private void canvas_Resize(object sender, EventArgs e)
        {
            canvas.Invalidate();
        }

        Rectangle[] GetScreenBounds()
        {
            return GetVirtualScreenBounds();
        }

        private void Reflesh_Click(object sender, EventArgs e)
        {
            RefleshVirtualScreenBounds();
        }

        Rectangle[] GetRealScreenBounds()
        {
            return Screen.AllScreens.Select(scr => scr.Bounds).ToArray();
        }

        Rectangle[] GetVirtualScreenBounds()
        {
            return virtualScreenBounds;
        }

        private void RefleshVirtualScreenBounds()
        {
            virtualScreenBounds = GetRealScreenBounds();
        }

        private void ctrlCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            shortcutKey = GetShortcutKey();
        }

        private void shiftCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            shortcutKey = GetShortcutKey();
        }

        private void altCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            shortcutKey = GetShortcutKey();
        }

        private void keyCombobox_SelectedIndexChanged(object sender, EventArgs e)
        {
            shortcutKey = GetShortcutKey();
        }

        ShortcutKey GetShortcutKey()
        {
            if (keyCombobox.Text.Length == 1)
            {
                if (keyCombobox.Text[0] < 'A' || 'Z' < keyCombobox.Text[0])
                    return null;
            }
            return new ShortcutKey(
                ctrlCheckbox.Checked,
                shiftCheckbox.Checked,
                altCheckbox.Checked,
                keyCombobox.Text);
        }
    }

    public class ShortcutKey
    {
        public bool ctrl = false;
        public bool shift = false;
        public bool alt = false;
        public string key = "";
        public ShortcutKey(bool ctrl, bool shift, bool alt, string key)
        {
            this.ctrl = ctrl;
            this.shift = shift;
            this.alt = alt;
            this.key = key;
        }
    }
}
