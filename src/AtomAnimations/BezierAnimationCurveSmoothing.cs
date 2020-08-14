using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class BezierAnimationCurveSmoothing
    {
        private float[] _k; // Knots
        private float[] _w; // Weights
        private float[] _p1; // Out
        private float[] _p2; // In
        private float[] _r; // rhs vector
        private float[] _a;
        private float[] _b;
        private float[] _c;

        public void AutoComputeControlPoints(List<BezierKeyframe> keys, bool loop)
        {
            // Original implementation: https://www.particleincell.com/wp-content/uploads/2012/06/bezier-spline.js
            // Based on: https://www.particleincell.com/2012/bezier-splines/
            // Using improvements on near keyframes: http://www.jacos.nl/jacos_html/spline/
            var n = keys.Count - 1;
            if (_k == null || _k.Length < n + 1)
            {
                _k = new float[n + 1];
                _w = new float[n + 1];
                _p1 = new float[n + 1];
                _p2 = new float[n + 1];
                // rhs vector
                _a = new float[n];
                _b = new float[n];
                _c = new float[n];
                _r = new float[n];
            }
            for (var i = 0; i <= n; i++)
            {
                _k[i] = keys[i].value;
                if (i < n) _w[i] = keys[i + 1].time - keys[i].time;
            }
            _w[_w.Length - 1] = keys[0].time - keys[_w.Length - 1].time;

            // internal segments
            for (var i = 0; i < n; i++)
            {
                var frac_i = _w[i] / _w[i + 1];
                _a[i] = 1 * _w[i] * _w[i];
                var prev_i = i == 0 ? n - 1 : i - 1;
                _b[i] = 2 * _w[prev_i] * (_w[prev_i] + _w[i]);
                _c[i] = _w[prev_i] * _w[prev_i] * frac_i; // W[i]/W[i+1];
                _r[i] = Mathf.Pow(_w[prev_i] + _w[i], 2) * _k[i] + Mathf.Pow(_w[prev_i], 2) * (1 + frac_i) * _k[i + 1];
            }

            // solves Ax=b with the Thomas algorithm
            ThomasCircular_new();

            /*we have p1, now compute p2*/
            // required: p1[n] exists
            _p1[n] = _p1[0];
            for (var i = 0; i < n; i++)
            {   //p2[i]=2*K[i+1]-p1[i+1];
                _p2[i] = _k[i + 1] * (1 + _w[i] / _w[i + 1]) - _p1[i + 1] * (_w[i] / _w[i + 1]);
            }

            // Assign the control points to the keyframes
            for (var i = 1; i < n - 1; i++)
            {
                keys[i].controlPointIn = _p2[i - 1];
                keys[i].controlPointOut = _p1[i];
            }
            if (loop)
            {
                var avgIn = (keys[0].controlPointIn + keys[keys.Count - 1].controlPointIn) / 2f;
                keys[0].controlPointIn = keys[keys.Count - 1].controlPointIn = avgIn;
                var avgOut = (keys[0].controlPointOut + keys[keys.Count - 1].controlPointOut) / 2f;
                keys[0].controlPointOut = keys[keys.Count - 1].controlPointOut = avgOut;
            }
        }
        private void ThomasCircular_new()
        /*solves Ax=r by Guassian elimination (for a matrix that has many zeros)
            r: right-hand vector
            a: array of the sub-diagonal elements (indexes 1 till n-1)
            a[0] is the most up most right element (position [0,n-1])
            b: array of the diagonal elements(indexes 0 till n-1)
            c: array of the upper-diagonal elements(indexed 0 till n-2)
            c[n-1] is the lowest most left element (position [n-1,0])
            all other elements are supposed to be 0
          |   b0 c0 0  0  ...    .      .      .      a0    |
          |   a1 b1 c1 0  ...    .      .      .      0     |
          |   0  a2 b2 c2 ..     .      .      .      0     |
          |   .   . .  .  ...    .      .      .      .     |  x = r
          |   .   . .  .  ...    .      .      .      .     |
          |   0   0 0  0  ... a[n-3] b[n-3] c[n-3]    0     |
          |   0   0 0  0  ...    0   a[n-2] b[n-2]  c[n-2]  |
          |c[n-1] 0 0  0  ...    0      0   a[n-1]  b[n-1]  |
        */
        {
            var n = _r.Length;

            // lastcolumn
            // for lc, indexes 0 till n-3 are used.
            // lc[n-2] is not used, use c[n-2]
            // lc[n-1] is not used, use b[n-1]
            var lc = new float[n];
            lc[0] = _a[0];

            // last row
            // lr contains a value from the last row
            // indexes 0 till n-3 are used.
            // lr[n-2] is not used, use a[n-1]
            // lr[n-1] is not used, use b[n-1]
            var lr = _c[n - 1];

            int i;
            for (i = 0; i < n - 3; i++)
            {
                var m = _a[i + 1] / _b[i];
                _b[i + 1] -= m * _c[i];
                _r[i + 1] -= m * _r[i];
                // last column // superflous when i=n-2
                lc[i + 1] = -m * lc[i];

                // last row : lr=lr[i]
                m = lr / _b[i];
                _b[n - 1] -= m * lc[i];
                lr = -m * _c[i]; // lr=lr[i+1], superflous when i=n-2
                                 // lr[i]=0 maar deze waarde wordt niet meer gebruikt
                _r[n - 1] -= m * _r[i];
            }
            // note that i = n-3 now
            {
                var m = _a[i + 1] / _b[i];
                _b[i + 1] -= m * _c[i];
                _r[i + 1] -= m * _r[i];
                // last column
                // in stead of lc[i+1]=-m*lc[i]
                _c[i + 1] -= m * lc[i];
                // last row
                m = lr / _b[i];
                _b[n - 1] -= m * lc[i];
                // in stead of lr[i+1]= - m * c[i] // superflous when i=n-2
                _a[n - 1] -= m * _c[i];
                // lr[i]=0 maar deze waarde wordt niet meer gebruikt
                _r[n - 1] = _r[n - 1] - m * _r[i];
            }
            i = n - 2;
            // in stead of: for (i = n-1; i < n; i++)
            {
                var m = _a[i + 1] / _b[i];
                // in stead of lr[i+1]= - m * c[i]
                _b[i + 1] -= m * _c[i];
                // in stead of r[n-1]= r[n-1] - m * r[i] // reeds gedaan
                _r[i + 1] -= m * _r[i];

                // last column
                // lc[i+1] wordt niet gebruikt, dit is b[i+1]

                // last row
                // lr[i] wordt niet gebruikt, dit is a[i+1]

                // m = lr[i]/b[i];
                // b[i+1] -=  m * c[i] // reeds gedaan
                // lr[i]=0
            }

            _p1[n - 1] = _r[n - 1] / _b[n - 1];
            // the value of lc[n-2] should not be used in the loop
            lc[n - 2] = 0;
            for (i = n - 2; i >= 0; --i)
            {
                _p1[i] = (_r[i] - _c[i] * _p1[i + 1] - lc[i] * _p1[n - 1]) / _b[i];
            }
        }
    }
}
