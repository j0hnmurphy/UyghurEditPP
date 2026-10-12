using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UyghurEditPP
{
	/// <summary>
	/// Scales what Windows Forms leaves at 100% in a tool strip: ImageScalingSize, the size of
	/// items that are not auto-sized, the images drawn at their own size (ImageScaling None) and
	/// the drop-down part of split buttons. .NET Framework 4.8 rescales only the margins and
	/// default fonts of tool strip items, at startup and on a DPI change.
	/// Everything is computed from the values at 100% times the total factor Windows Forms has
	/// applied to the layout (as in KenjiTab), so changes back and forth do not drift.
	/// </summary>
	internal sealed class ToolStripScaler
	{
		// ToolStripSplitButton's default drop-down width at 100%; Windows Forms scales its
		// default only once, by the system DPI.
		internal const int DropDownButtonWidth = 11;

		readonly ToolStrip _strip;
		SizeF _scale = new SizeF(1f, 1f);
		Size _logicalImageScalingSize, _appliedImageScalingSize;
		bool _haveImageScalingSize;
		readonly Dictionary<ToolStripItem, Size> _logicalSizes = new Dictionary<ToolStripItem, Size>();
		readonly Dictionary<ToolStripItem, Size> _appliedSizes = new Dictionary<ToolStripItem, Size>();
		readonly Dictionary<ToolStripItem, Image> _originalImages = new Dictionary<ToolStripItem, Image>();
		readonly Dictionary<ToolStripItem, Image> _scaledImages = new Dictionary<ToolStripItem, Image>();

		internal ToolStripScaler(ToolStrip strip)
		{
			_strip = strip;
		}

		internal SizeF Factor { get { return _scale; } }

		/// <summary>Called from ScaleControl; see KenjiTab.ScaleControl for why `specified` matters.</summary>
		internal void Scale(SizeF factor, BoundsSpecified specified)
		{
			bool width = (specified & BoundsSpecified.Width) != 0;
			bool height = (specified & BoundsSpecified.Height) != 0;
			if (!width && !height)
				return;
			Remember();
			_scale = new SizeF(width ? _scale.Width * factor.Width : _scale.Width, height ? _scale.Height * factor.Height : _scale.Height);
			Apply();
		}

		// The values at 100%: taken the first time, and again for anything set from outside since.
		internal void Remember()
		{
			if (!_haveImageScalingSize || _strip.ImageScalingSize != _appliedImageScalingSize)
			{
				_logicalImageScalingSize = Unscaled(_strip.ImageScalingSize);
				_appliedImageScalingSize = _strip.ImageScalingSize;
				_haveImageScalingSize = true;
			}
			foreach (ToolStripItem item in _strip.Items)
			{
				Size applied;
				if (!item.AutoSize && (!_appliedSizes.TryGetValue(item, out applied) || applied != item.Size))
					_logicalSizes[item] = Unscaled(item.Size);
				Image scaled;
				if (item.ImageScaling == ToolStripItemImageScaling.None && item.Image != null
					&& (!_scaledImages.TryGetValue(item, out scaled) || !ReferenceEquals(scaled, item.Image)))
				{
					// The image as set (at 100%, or replaced since): keep it to scale from.
					Image original;
					if (scaled != null && !(_originalImages.TryGetValue(item, out original) && ReferenceEquals(scaled, original)))
						scaled.Dispose();   // our scaled copy of the image that was replaced
					_originalImages[item] = item.Image;
					_scaledImages.Remove(item);
				}
			}
		}

		/// <summary>
		/// ToolStrip.RescaleConstantsForDpi (on a DPI change of the window, and when the handle is
		/// created on a monitor whose DPI is not the system DPI) calls ResetScaling, which sets
		/// ImageScalingSize to 16 px at the new DPI. Put back the size this strip is laid out for.
		/// </summary>
		internal void RestoreImageScalingSize()
		{
			if (!_haveImageScalingSize)
				return;
			_strip.ImageScalingSize = Scaled(_logicalImageScalingSize);
			_appliedImageScalingSize = _strip.ImageScalingSize;
		}

		void Apply()
		{
			_strip.SuspendLayout();
			try
			{
				_strip.ImageScalingSize = Scaled(_logicalImageScalingSize);
				_appliedImageScalingSize = _strip.ImageScalingSize;
				foreach (ToolStripItem item in _strip.Items)
				{
					Size logical;
					if (!item.AutoSize && _logicalSizes.TryGetValue(item, out logical))
					{
						item.Size = Scaled(logical);
						_appliedSizes[item] = item.Size;
					}
					ToolStripSplitButton split = item as ToolStripSplitButton;
					if (split != null)
						split.DropDownButtonWidth = Scaled(DropDownButtonWidth, _scale.Width);
					Image original;
					if (_originalImages.TryGetValue(item, out original))
						SetImage(item, original);
				}
			}
			finally
			{
				_strip.ResumeLayout(true);
			}
		}

		void SetImage(ToolStripItem item, Image original)
		{
			Size size = Scaled(original.Size);
			Image old;
			_scaledImages.TryGetValue(item, out old);
			Image image = size == original.Size ? original : ScaleImage(original, size, item.ImageTransparentColor);
			item.Image = image;
			_scaledImages[item] = image;
			if (old != null && !ReferenceEquals(old, original) && !ReferenceEquals(old, image))
				old.Dispose();
		}

		/// <summary>
		/// The image at another size. High-quality bicubic, like Windows' own stretching of
		/// programs that are not DPI aware; the transparent color is made transparent first so
		/// it does not bleed into the edges.
		/// </summary>
		internal static Bitmap ScaleImage(Image source, Size size, Color transparent)
		{
			using (Bitmap src = new Bitmap(source))
			{
				if (!transparent.IsEmpty)
					src.MakeTransparent(transparent);
				Bitmap dst = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				using (Graphics g = Graphics.FromImage(dst))
				{
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;
					g.CompositingMode = CompositingMode.SourceCopy;
					using (System.Drawing.Imaging.ImageAttributes clamp = new System.Drawing.Imaging.ImageAttributes())
					{
						clamp.SetWrapMode(WrapMode.TileFlipXY); // no half-transparent border from outside the image
						g.DrawImage(src, new Rectangle(Point.Empty, size), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, clamp);
					}
				}
				return dst;
			}
		}

		Size Scaled(Size s) { return new Size(Scaled(s.Width, _scale.Width), Scaled(s.Height, _scale.Height)); }
		Size Unscaled(Size s) { return new Size(Unscaled(s.Width, _scale.Width), Unscaled(s.Height, _scale.Height)); }

		static int Scaled(int value, float scale)
		{
			return (int)Math.Round(value * (double)scale, MidpointRounding.AwayFromZero);
		}

		static int Unscaled(int value, float scale)
		{
			return (int)Math.Round(value / (double)scale, MidpointRounding.AwayFromZero);
		}
	}

	/// <summary>A ToolStrip whose buttons and images follow the DPI (ToolStripScaler).</summary>
	public class KenjiToolStrip : ToolStrip
	{
		internal readonly ToolStripScaler Scaler;

		public KenjiToolStrip()
		{
			Scaler = new ToolStripScaler(this);
		}

		protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
		{
			base.ScaleControl(factor, specified);
			Scaler.Scale(factor, specified);
		}

		protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
		{
			Scaler.Remember();   // the sizes as they are, before ResetScaling overwrites ImageScalingSize
			base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
			Scaler.RestoreImageScalingSize();
		}
	}
}
