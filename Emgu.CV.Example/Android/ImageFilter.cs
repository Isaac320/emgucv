using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;

namespace Emgu.CV
{
   public abstract class ImageFilter : Emgu.Util.DisposableObject, ICloneable
   {
      protected ImageBufferFactory<Image<Bgr, Byte>> _bgrBuffers;
      protected ImageBufferFactory<Image<Gray, Byte>> _grayBuffers;
      protected ImageBufferFactory<Image<Bgr, Single>> _bgrSingleBuffers;
      protected bool _inplaceCapable = false;

      public ImageFilter()
      {
      }

      /// <summary>
      /// If true, the sourceImage and destImage in ProcessData function can be the same
      /// </summary>
      public bool InplaceCapable
      {
         get
         {
            return _inplaceCapable;
         }
      }

      public abstract void ProcessData(Image<Bgr, Byte> sourceImage, Image<Bgr, Byte> destImage);

      public Image<Bgr, Single> GetBufferBgrSingle(Size size, int index)
      {
         if (_bgrSingleBuffers == null)
            _bgrSingleBuffers = new ImageBufferFactory<Image<Bgr, Single>>(s => new Image<Bgr, Single>(s));
         return _bgrSingleBuffers.GetBuffer(size, index);
      }

      public Image<Bgr, Byte> GetBufferBgr(Size size, int index)
      {
         if (_bgrBuffers == null)
            _bgrBuffers = new ImageBufferFactory<Image<Bgr, Byte>>( s => new Image<Bgr, Byte>(s));
         return _bgrBuffers.GetBuffer(size, index);
      }

      public Image<Gray, Byte> GetBufferGray(Size size, int index)
      {
         if (_grayBuffers == null)
            _grayBuffers = new ImageBufferFactory<Image<Gray, Byte>>( s => new Image<Gray, Byte>(s));
         return _grayBuffers.GetBuffer(size, index);
      }

      protected override void DisposeObject()
      {
         if (_bgrBuffers != null)
         {
            _bgrBuffers.Dispose();
            _bgrBuffers = null;
         }
         if (_grayBuffers != null)
         {
            _grayBuffers.Dispose();
            _grayBuffers = null;
         }
         if (_bgrSingleBuffers != null)
         {
            _bgrSingleBuffers.Dispose();
            _bgrSingleBuffers = null;
         }
      }

      public abstract Object Clone();
   }

   public class CannyFilter : ImageFilter
   {
      private double _thresh;
      private double _threshLinking;
      private int _apertureSize;

      public CannyFilter(double thresh, double threshLinking, int apertureSize)
      {
         _thresh = thresh;
         _threshLinking = threshLinking;
         _apertureSize = apertureSize;

         _inplaceCapable = true;
      }

      public override void ProcessData(Image<Bgr, Byte> sourceImage, Image<Bgr, Byte> destImage)
      {
         Size size = sourceImage.Size;

         Image<Gray, Byte> i0 = GetBufferGray(size, 0);
         Image<Gray, Byte> i1 = GetBufferGray(size, 1);
         Image<Gray, Byte> i2 = GetBufferGray(size, 2);
         Image<Gray, Byte> i3 = GetBufferGray(size, 3);
         //Image<Gray, Byte> gCanny = GetBufferGray(size, 4);
         //Image<Gray, Byte> rCanny = GetBufferGray(size, 5);

         CvInvoke.cvSplit(sourceImage, i1, i2, i3, IntPtr.Zero);
         CvInvoke.cvCanny(i1, i0, _thresh, _threshLinking, _apertureSize);
         CvInvoke.cvCanny(i2, i1, _thresh, _threshLinking, _apertureSize);
         CvInvoke.cvCanny(i3, i2, _thresh, _threshLinking, _apertureSize);
         CvInvoke.cvMerge(i0, i1, i2, IntPtr.Zero, destImage);
      }

      public override object Clone()
      {
         return new CannyFilter(_thresh, _threshLinking, _apertureSize);
      }
   }

   public class ColorMapFilter : ImageFilter
   {
      private Emgu.CV.CvEnum.ColorMapType _colorMapType;

      public ColorMapFilter(Emgu.CV.CvEnum.ColorMapType type)
      {
         _colorMapType = type;
         _inplaceCapable = true;
      }

      public override void ProcessData(Image<Bgr, Byte> sourceImage, Image<Bgr, Byte> destImage)
      {
         CvInvoke.ApplyColorMap(sourceImage, destImage, _colorMapType);
      }

      public override object Clone()
      {
         return new ColorMapFilter(_colorMapType);
      }
   }

   public class DistorFilter : ImageFilter
   {
      private double _centerX;
      private double _centerY;
      private double _distorCoeff;

      private Matrix<float> _mapX;
      private Matrix<float> _mapY;

      private Size _size;

      /// <summary>
      /// 
      /// </summary>
      /// <param name="centerWidth">A value between 0 and 1.0, if 0, the center is on the left side of the image, if 1, the center is on the right side of the image</param>
      /// <param name="centerHeight">A value between 0 and 1.0, if 0, the center is on the top of the image, if 1, the center is on the bottom of the image</param>
      /// <param name="distorCoeff"></param>
      public DistorFilter(double centerWidth, double centerHeight, double distorCoeff)
      {
         if (centerWidth < 0 || centerWidth > 1.0 || centerHeight < 0 || centerHeight > 1.0)
         {
            throw new ArgumentException("CenterWidth and CenterHeight must be a number >= 0 and <= 1.0");
         }
         _centerX = centerWidth;
         _centerY = centerHeight;
         _distorCoeff = distorCoeff;
      }

      public override void ProcessData(Image<Bgr, Byte> sourceImage, Image<Bgr, Byte> destImage)
      {
         if (!sourceImage.Size.Equals(_size))
         {
            if (_mapX != null)
            {
               _mapX.Dispose();
               _mapX = null;
            }
            if (_mapY != null)
            {
               _mapY.Dispose();
               _mapY = null;
            }

            _size = sourceImage.Size;
         }

         if (_mapX == null || _mapY == null)
         {
            IntrinsicCameraParameters p = new IntrinsicCameraParameters(5);
            int centerY = (int)(_size.Width * _centerY);
            int centerX = (int)(_size.Height * _centerX);
            CvInvoke.cvSetIdentity(p.IntrinsicMatrix, new MCvScalar(1.0));
            p.IntrinsicMatrix.Data[0, 2] = centerY;
            p.IntrinsicMatrix.Data[1, 2] = centerX;
            p.IntrinsicMatrix.Data[2, 2] = 1;
            p.DistortionCoeffs.Data[0, 0] = _distorCoeff / (_size.Width * _size.Height);

            p.InitUndistortMap(_size.Width, _size.Height, out _mapX, out _mapY);
         }

         CvInvoke.cvRemap(sourceImage, destImage, _mapX, _mapY, (int)Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR | (int)Emgu.CV.CvEnum.WARP.CV_WARP_FILL_OUTLIERS, new MCvScalar());
      }

      public override object Clone()
      {
         return new DistorFilter(_centerX, _centerY, _distorCoeff);
      }
   }
}