using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace WiimoteWhiteboard
{
    class Warper
    {
        PointF[] src = new PointF[4];
        PointF[] dst = new PointF[4];

        float[] srcMat = new float[16];
        float[] dstMat = new float[16];
        float[] warpMat = new float[16];
	    bool dirty;

        public Warper()
        {
            setIdentity();
        }

        public void setIdentity()
        {
            setSource(new PointF(0.0f, 0.0f),
                      new PointF(1.0f, 0.0f),
                      new PointF(0.0f, 1.0f),
                      new PointF(1.0f, 1.0f));
            setDestination(new PointF(0.0f, 0.0f),
                      new PointF(1.0f, 0.0f),
                      new PointF(0.0f, 1.0f),
                      new PointF(1.0f, 1.0f));
            computeWarp();
        }

        public void setSource(PointF p1, PointF p2, PointF p3, PointF p4)
        {
            src[0] = p1;
            src[1] = p2;
            src[2] = p3;
            src[3] = p4;
            dirty = true;
        }

        public void setDestination(PointF p1, PointF p2, PointF p3, PointF p4)
        {
            dst[0] = p1;
            dst[1] = p2;
            dst[2] = p3;
            dst[3] = p4;
            dirty = true;
        }


        public void computeWarp()
        {
	        computeQuadToSquare(src[0], src[1], src[2], src[3], srcMat);
	        computeSquareToQuad(dst[0],	dst[1], dst[2], dst[3], dstMat);
	        multMats(srcMat, dstMat, warpMat);
	        dirty = false;
        }

        public void multMats(float[] srcMat, float[] dstMat, float[] resMat) {
	        // DSTDO/CBB: could be faster, but not called often enough to matter
	        for (int r = 0; r < 4; r++) {
	            int ri = r * 4;
	            for (int c = 0; c < 4; c++) {
		        resMat[ri + c] = (srcMat[ri    ] * dstMat[c     ] +
				          srcMat[ri + 1] * dstMat[c +  4] +
				          srcMat[ri + 2] * dstMat[c +  8] +
				          srcMat[ri + 3] * dstMat[c + 12]);
			        }
		        }
            }

        public void computeSquareToQuad(PointF p1, PointF p2, PointF p3, PointF p4, float[] mat)
        {
            PointF d1 = new PointF();
            d1.X = p2.X - p3.X; d1.Y = p2.Y - p3.Y;
            PointF d2 = new PointF();
            d2.X = p4.X - p3.X; d2.Y = p4.Y - p3.Y;

            PointF s = new PointF();
            s.X = p1.X - p2.X + p3.X - p4.X;
            s.Y = p1.Y - p2.Y + p3.Y - p4.Y;

	        float g = (s.X * d2.Y - d2.X * s.Y) / (d1.X * d2.Y - d2.X * d1.Y);
	        float h = (d1.X * s.Y - s.X * d1.Y) / (d1.X * d2.Y - d2.X * d1.Y);
	        float a = p2.X - p1.X + g * p2.X;
	        float b = p4.X - p1.X + h * p4.X;
	        float c = p1.X;
	        float d = p2.Y - p1.Y + g * p2.Y;
	        float e = p4.Y - p1.Y + h * p4.Y;
	        float f = p1.Y;

	        mat[ 0] = a;	mat[ 1] = d;	mat[ 2] = 0;	mat[ 3] = g;
	        mat[ 4] = b;	mat[ 5] = e;	mat[ 6] = 0;	mat[ 7] = h;
	        mat[ 8] = 0;	mat[ 9] = 0;	mat[10] = 1;	mat[11] = 0;
	        mat[12] = c;	mat[13] = f;	mat[14] = 0;	mat[15] = 1;
        }

        public void computeQuadToSquare(PointF p1, PointF p2, PointF p3, PointF p4, float[] mat)
        {
	        computeSquareToQuad(p1, p2, p3, p4, mat);

	        // invert through adjoint

	        float a = mat[ 0],	d = mat[ 1],	/* ignore */		g = mat[ 3];
	        float b = mat[ 4],	e = mat[ 5],	/* 3rd col*/		h = mat[ 7];
	        /* ignore 3rd row */
	        float c = mat[12],	f = mat[13];

	        float A =     e - f * h;
	        float B = c * h - b;
	        float C = b * f - c * e;
	        float D = f * g - d;
	        float E =     a - c * g;
	        float F = c * d - a * f;
	        float G = d * h - e * g;
	        float H = b * g - a * h;
	        float I = a * e - b * d;

	        // Probably unnecessary since 'I' is also scaled by the determinant,
	        //   and 'I' scales the homogeneous coordinate, which, in turn,
	        //   scales the X,Y coordinates.
	        // Determinant  =   a * (e - f * h) + b * (f * g - d) + c * (d * h - e * g);
	        float idet = 1.0f / (a * A           + b * D           + c * G);

	        mat[ 0] = A * idet;	mat[ 1] = D * idet;	mat[ 2] = 0;	mat[ 3] = G * idet;
	        mat[ 4] = B * idet;	mat[ 5] = E * idet;	mat[ 6] = 0;	mat[ 7] = H * idet;
	        mat[ 8] = 0       ;	mat[ 9] = 0       ;	mat[10] = 1;	mat[11] = 0       ;
	        mat[12] = C * idet;	mat[13] = F * idet;	mat[14] = 0;	mat[15] = I * idet;
        }

        public float[] getWarpMatrix()
        {
	        return warpMat;
        }

        public void warp(PointF src, ref PointF dst)
        {
            if (dirty)
                computeWarp();
            Warper.warp(warpMat, src, ref dst);
        }

        public static void warp(float[] mat, PointF src, ref PointF dst)
        {
            float[] result = new float[4];
            float z = 0;
            result[0] = (float)(src.X * mat[0] + src.Y * mat[4] + z * mat[8] + 1 * mat[12]);
            result[1] = (float)(src.X * mat[1] + src.Y * mat[5] + z * mat[9] + 1 * mat[13]);
            result[2] = (float)(src.X * mat[2] + src.Y * mat[6] + z * mat[10] + 1 * mat[14]);
            result[3] = (float)(src.X * mat[3] + src.Y * mat[7] + z * mat[11] + 1 * mat[15]);        
            dst.X = result[0]/result[3];
		    dst.Y = result[1]/result[3];
        }
    }
}
