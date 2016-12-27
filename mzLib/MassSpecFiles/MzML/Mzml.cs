﻿// Copyright 2012, 2013, 2014 Derek J. Bailey
// Modified work Copyright 2016 Stefan Solntsev
//
// This file (Mzml.cs) is part of MassSpecFiles.
//
// MassSpecFiles is free software: you can redistribute it and/or modify it
// under the terms of the GNU Lesser General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MassSpecFiles is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public
// License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with MassSpecFiles. If not, see <http://www.gnu.org/licenses/>.

using Ionic.Zlib;
using MassSpectrometry;
using Spectra;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace IO.MzML
{
    public class Mzml : MsDataFile<DefaultMzSpectrum>
    {
        private static string _msnOrderAccession = "MS:1000511";
        private static string _precursorCharge = "MS:1000041";
        private static string _precursorMass = "MS:1000744";
        private static string _isolationWindowTargetMZ = "MS:1000827";
        private static string _isolationWindowLowerOffset = "MS:1000828";
        private static string _isolationWindowUpperOffset = "MS:1000829";
        private static string _retentionTime = "MS:1000016";
        private static string _ionInjectionTime = "MS:1000927";
        private static string _mzArray = "MS:1000514";
        private static string _intensityArray = "MS:1000515";
        private const string _CID = "MS:1000133";
        private const string _ISCID = "MS:1001880";
        private const string _HCD = "MS:1000422";
        private const string _ETD = "MS:1000598";
        private const string _MPD = "MS:1000435";
        private const string _ECD = "MS:1000250";
        private const string _PQD = "MS:1000599";
        private const string _DefaultDissociation = "MS:1000044";
        private const string _quadrupole = "MS:1000081";
        private const string _linearIonTrap = "MS:1000291";
        private const string _IonTrap2DAxialEject = "MS:1000078";
        private const string _IonTrap2DRadialEject = "MS:1000083";
        private const string _IonTrap3D = "MS:1000082";
        private const string _orbitrap = "MS:1000484";
        private const string _TOF = "MS:1000084";
        private const string _FTICR = "MS:1000079";
        private const string _magneticSector = "MS:1000080";
        private const string _nozlibCompress = "MS:1000576";
        private const string _zlibCompression = "MS:1000574";
        private const string _64bit = "MS:1000523";
        private const string _32bit = "MS:1000521";
        private const string _negativePolarity = "MS:1000129";
        private const string _positivePolarity = "MS:1000130";
        private const string _filterString = "MS:1000512";
        private const string _centroidSpectrum = "MS:1000127";
        private const string _profileSpectrum = "MS:1000128";
        private const string _peakIntensity = "MS:1000042";
        private const string _totalIonCurrent = "MS:1000285";
        private const string _scanWindowLowerLimit = "MS:1000501";
        private const string _scanWindowUpperLimit = "MS:1000500";

        private static XmlSerializer _indexedSerializer = new XmlSerializer(typeof(Generated.indexedmzML));
        private static XmlSerializer _mzMLSerializer = new XmlSerializer(typeof(Generated.mzMLType));

        private Generated.indexedmzML _indexedmzMLConnection;
        private Generated.mzMLType _mzMLConnection;

        public Mzml(string filePath)
            : base(filePath, true, MsDataFileType.Mzml)
        {
        }

        public override void Open()
        {
            if (_mzMLConnection == null)
            {
                Stream stream = new FileStream(FilePath, FileMode.Open);
                try
                {
                    _indexedmzMLConnection = _indexedSerializer.Deserialize(stream) as Generated.indexedmzML;
                    _mzMLConnection = _indexedmzMLConnection.mzML;
                }
                catch (Exception)
                {
                    try
                    {
                        _mzMLConnection = _mzMLSerializer.Deserialize(stream) as Generated.mzMLType;
                    }
                    catch (Exception)
                    {
                        throw new InvalidDataException("Unable to parse " + FilePath + " as an mzML file!");
                    }
                }
            }
        }

        public static void Write(string filePath, Generated.indexedmzML _indexedmzMLConnection)
        {
            TextWriter writer = new StreamWriter(filePath);
            _indexedSerializer.Serialize(writer, _indexedmzMLConnection);
            writer.Close();
        }

        public bool IsIndexedMzML
        {
            get { return _indexedmzMLConnection != null; }
        }

        private DissociationType GetDissociationType(int oneBasedSpectrumNumber)
        {
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].precursorList.precursor[0].activation.cvParam)
            {
                switch (cv.accession)
                {
                    case _CID:
                        return DissociationType.CID;

                    case _ISCID:
                        return DissociationType.ISCID;

                    case _HCD:
                        return DissociationType.HCD;

                    case _ETD:
                        return DissociationType.ETD;

                    case _MPD:
                        return DissociationType.MPD;

                    case _PQD:
                        return DissociationType.PQD;

                    case _DefaultDissociation:
                        return DissociationType.Unknown;
                }
            }
            throw new ArgumentNullException("Could not find dissociation type for spectrum number " + oneBasedSpectrumNumber);
        }

        private int GetMsnOrder(int oneBasedspectrumNumber)
        {
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedspectrumNumber - 1].cvParam)
            {
                if (cv.accession.Equals(_msnOrderAccession))
                {
                    return int.Parse(cv.value);
                }
            }
            throw new ArgumentNullException("Could not find MSn level for spectrum number " + oneBasedspectrumNumber);
        }

        // ZERO MEANS UNKNOWN CHARGE STATE, NOT ACTUALLY ZERO!!!
        private int GetPrecusorCharge(int oneBasedSpectrumNumber)
        {
            if (_mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].precursorList == null)
                return 0;

            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam)
            {
                if (cv.accession.Equals(_precursorCharge))
                {
                    return short.Parse(cv.value);
                }
            }
            return 0;
        }

        private MzRange GetScanWindowMzRange(int oneBasedSpectrumNumber)
        {
            double high = double.NaN;
            double low = double.NaN;

            if (_mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].scanList.scan[0].scanWindowList != null)
                foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].scanList.scan[0].scanWindowList.scanWindow[0].cvParam)
                {
                    if (cv.accession.Equals(_scanWindowLowerLimit))
                    {
                        low = double.Parse(cv.value);
                    }
                    if (cv.accession.Equals(_scanWindowUpperLimit))
                    {
                        high = double.Parse(cv.value);
                    }
                }
            return new MzRange(low, high);
        }

        private double GetIsolationWidth(int oneBasedSpectrumNumber)
        {
            double low = double.NaN;
            double high = double.NaN;

            if (_mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].precursorList.precursor[0].isolationWindow == null)
                return double.NaN;

            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].precursorList.precursor[0].isolationWindow.cvParam)
            {
                if (cv.accession.Equals(_isolationWindowLowerOffset))
                {
                    low = double.Parse(cv.value);
                }
                if (cv.accession.Equals(_isolationWindowUpperOffset))
                {
                    high = double.Parse(cv.value);
                }
            }
            if (double.IsNaN(low) || double.IsNaN(high))
            {
                throw new ArgumentNullException("Could not determine isolation width for " + oneBasedSpectrumNumber);
            }
            return high - low;
        }

        private string GetOneBasedScanFilter(int oneBasedSpectrumNumber)
        {
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].scanList.scan[0].cvParam)
            {
                if (cv.accession.Equals(_filterString))
                {
                    return cv.value;
                }
            }
            // Not a problem if null, scan filter is optional!
            return null;
        }

        private static readonly Regex MZAnalyzerTypeRegex = new Regex(@"^[a-zA-Z]*", RegexOptions.Compiled);

        private MZAnalyzerType GetMzAnalyzer(int oneBasedSpectrumNumber)
        {
            string filter = GetOneBasedScanFilter(oneBasedSpectrumNumber);

            if (filter == null)
                return MZAnalyzerType.Unknown;

            string type = MZAnalyzerTypeRegex.Match(filter).Captures[0].Value;

            switch (type)
            {
                case "ITMS":
                    return MZAnalyzerType.IonTrap2D;

                case "TQMS":
                    return MZAnalyzerType.Unknown;

                case "SQMS":
                    return MZAnalyzerType.Unknown;

                case "TOFMS":
                    return MZAnalyzerType.TOF;

                case "FTMS":
                    return MZAnalyzerType.Orbitrap;

                case "Sector":
                    return MZAnalyzerType.Sector;
            }

            // Maybe in the beginning of the file, there is a single analyzer?
            // Gets the first analyzer used.
            string analyzer = _mzMLConnection.instrumentConfigurationList.instrumentConfiguration[0].cvParam[0].accession;

            switch (analyzer)
            {
                case _quadrupole:
                    return MZAnalyzerType.Quadrupole;

                case _linearIonTrap:
                    return MZAnalyzerType.IonTrap2D;

                case _IonTrap3D:
                    return MZAnalyzerType.IonTrap3D;

                case _orbitrap:
                    return MZAnalyzerType.Orbitrap;

                case _TOF:
                    return MZAnalyzerType.TOF;

                case _FTICR:
                    return MZAnalyzerType.FTICR;

                case _magneticSector:
                    return MZAnalyzerType.Sector;

                default:
                    return MZAnalyzerType.Unknown;
            }
        }

        private Polarity GetPolarity(int oneBasedSpectrumNumber)
        {
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].cvParam)
            {
                if (cv.accession.Equals(_negativePolarity))
                {
                    return Polarity.Negative;
                }
                if (cv.accession.Equals(_positivePolarity))
                {
                    return Polarity.Positive;
                }
            }
            //return Polarity.Neutral;
            throw new ArgumentNullException("Could not find polarity for spectrum number " + oneBasedSpectrumNumber);
        }

        private double GetRetentionTime(int oneBasedSpectrumNumber)
        {
            if (_mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].scanList.scan[0].cvParam == null)
            {
                return double.NaN;
            }
            double rt = -1;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].scanList.scan[0].cvParam)
            {
                if (cv.accession.Equals(_retentionTime))
                {
                    rt = double.Parse(cv.value);
                }

                if (cv.unitName == "second")
                    rt /= 60;
            }

            if (rt >= 0)
                return rt;

            throw new ArgumentNullException("Could not determine retention time for " + oneBasedSpectrumNumber);
        }

        private double GetInjectionTime(int oneBasedSpectrumNumber)
        {
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[oneBasedSpectrumNumber - 1].scanList.scan[0].cvParam)
            {
                if (cv.accession.Equals(_ionInjectionTime))
                {
                    return double.Parse(cv.value);
                }
            }
            // HACK
            return -1;
        }

        protected override int GetNumSpectra()
        {
            return Convert.ToInt32(_mzMLConnection.run.spectrumList.count);
        }

        public override int GetClosestOneBasedSpectrumNumber(double retentionTime)
        {
            // TODO need to convert this to a binary search of some sort. Or if the data is indexedMZML see if the indices work better.
            int totalSpectra = Convert.ToInt32(_mzMLConnection.run.spectrumList.count);
            double bestDiff = double.MaxValue;
            for (int i = 0; i < totalSpectra; i++)
            {
                foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[i].scanList.scan[0].cvParam)
                {
                    if (cv.accession.Equals(_retentionTime))
                    {
                        double diff = Math.Abs(double.Parse(cv.value) - retentionTime);
                        if (diff > bestDiff)
                            return i;
                        else
                            bestDiff = diff;
                    }
                }
            }
            return totalSpectra;
        }

        public static byte[] ConvertDoublestoBase64(double[] toConvert, bool zlibCompressed)
        {
            var mem = new MemoryStream();
            for (int i = 0; i < toConvert.Length; i++)
            {
                byte[] ok = BitConverter.GetBytes(toConvert[i]);
                mem.Write(ok, 0, ok.Length);
            }
            mem.Position = 0;

            byte[] bytes = mem.ToArray();
            if (zlibCompressed)
                bytes = ZlibStream.CompressBuffer(bytes);

            return bytes;
        }

        /// <summary>
        /// Converts a 64-based encoded byte array into an double[]
        /// </summary>
        /// <param name="bytes">the 64-bit encoded byte array</param>
        /// <param name="zlibCompressed">Specifies if the byte array is zlib compressed</param>
        /// <returns>a decompressed, de-encoded double[]</returns>
        private static double[] ConvertBase64ToDoubles(byte[] bytes, bool zlibCompressed = false, bool is32bit = true)
        {
            // Add capability of compressed data
            if (zlibCompressed)
                bytes = ZlibStream.UncompressBuffer(bytes);

            int size = is32bit ? sizeof(float) : sizeof(double);

            int length = bytes.Length / size;
            double[] convertedArray = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (is32bit)
                {
                    convertedArray[i] = BitConverter.ToSingle(bytes, i * size);
                }
                else
                {
                    convertedArray[i] = BitConverter.ToDouble(bytes, i * size);
                }
            }
            return convertedArray;
        }

        private bool GetIsCentroid(int spectrumNumber)
        {
            spectrumNumber--;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].cvParam)
            {
                if (cv.accession.Equals(_centroidSpectrum))
                {
                    return true;
                }
                if (cv.accession.Equals(_profileSpectrum))
                {
                    return false;
                }
            }
            throw new ArgumentNullException("Could not determine if spectrum " + spectrumNumber + 1 + " is centroid or profile");
        }

        private string GetSpectrumID(int spectrumNumber)
        {
            spectrumNumber--;
            return _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].id;
        }

        private string GetPrecursorID(int spectrumNumber)
        {
            spectrumNumber--;
            return _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].precursorList.precursor[0].spectrumRef;
        }

        private double GetPrecursorIntensity(int spectrumNumber)
        {
            spectrumNumber--;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam)
            {
                if (cv.accession.Equals(_peakIntensity))
                {
                    return Convert.ToDouble(cv.value);
                }
            }
            return double.NaN;
        }

        private double GetPrecursorMz(int spectrumNumber)
        {
            spectrumNumber--;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam)
            {
                if (cv.accession.Equals(_precursorMass))
                {
                    return double.Parse(cv.value);
                }
            }
            throw new ArgumentNullException("Could not determine precursor monoisotopic mass for " + spectrumNumber + 1);
        }

        private double GetIsolationMz(int spectrumNumber)
        {
            spectrumNumber--;
            if (_mzMLConnection.run.spectrumList.spectrum[spectrumNumber].precursorList.precursor[0].isolationWindow == null)
                return double.NaN;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].precursorList.precursor[0].isolationWindow.cvParam)
            {
                if (cv.accession.Equals(_isolationWindowTargetMZ))
                {
                    return double.Parse(cv.value);
                }
            }
            return double.NaN;
        }

        protected override MsDataScan<DefaultMzSpectrum> GetMsDataOneBasedScanFromFile(int OneBasedSpectrumNumber)
        {
            double[] masses = null;
            double[] intensities = null;

            foreach (Generated.BinaryDataArrayType binaryData in _mzMLConnection.run.spectrumList.spectrum[OneBasedSpectrumNumber - 1].binaryDataArrayList.binaryDataArray)
            {
                bool compressed = false;
                bool mzArray = false;
                bool intensityArray = false;
                bool is32bit = true;
                foreach (Generated.CVParamType cv in binaryData.cvParam)
                {
                    if (cv.accession.Equals(_zlibCompression))
                    {
                        compressed = true;
                    }
                    if (cv.accession.Equals(_64bit))
                    {
                        is32bit = false;
                    }
                    if (cv.accession.Equals(_32bit))
                    {
                        is32bit = true;
                    }
                    if (cv.accession.Equals(_mzArray))
                    {
                        mzArray = true;
                    }
                    if (cv.accession.Equals(_intensityArray))
                    {
                        intensityArray = true;
                    }
                }

                double[] data = ConvertBase64ToDoubles(binaryData.binary, compressed, is32bit);
                if (mzArray)
                {
                    masses = data;
                }

                if (intensityArray)
                {
                    intensities = data;
                }
            }

            if (masses == null || intensities == null)
            {
                throw new InvalidDataException("Unable to find spectral data for spectrum number " + OneBasedSpectrumNumber);
            }

            var ok = new DefaultMzSpectrum(masses, intensities, false);

            if (GetMsnOrder(OneBasedSpectrumNumber) == 1)
                return new MsDataScan<DefaultMzSpectrum>(OneBasedSpectrumNumber, ok, GetSpectrumID(OneBasedSpectrumNumber), GetMsnOrder(OneBasedSpectrumNumber), GetIsCentroid(OneBasedSpectrumNumber), GetPolarity(OneBasedSpectrumNumber), GetRetentionTime(OneBasedSpectrumNumber), GetScanWindowMzRange(OneBasedSpectrumNumber), GetOneBasedScanFilter(OneBasedSpectrumNumber), GetMzAnalyzer(OneBasedSpectrumNumber), GetInjectionTime(OneBasedSpectrumNumber), GetTotalIonCurrent(OneBasedSpectrumNumber));
            else
                return new MsDataScan<DefaultMzSpectrum>(OneBasedSpectrumNumber, ok, GetSpectrumID(OneBasedSpectrumNumber), GetMsnOrder(OneBasedSpectrumNumber), GetIsCentroid(OneBasedSpectrumNumber), GetPolarity(OneBasedSpectrumNumber), GetRetentionTime(OneBasedSpectrumNumber), GetScanWindowMzRange(OneBasedSpectrumNumber), GetOneBasedScanFilter(OneBasedSpectrumNumber), GetMzAnalyzer(OneBasedSpectrumNumber), GetInjectionTime(OneBasedSpectrumNumber), GetTotalIonCurrent(OneBasedSpectrumNumber), GetPrecursorID(OneBasedSpectrumNumber), GetPrecursorMz(OneBasedSpectrumNumber), GetPrecusorCharge(OneBasedSpectrumNumber), GetPrecursorIntensity(OneBasedSpectrumNumber), GetIsolationMz(OneBasedSpectrumNumber), GetIsolationWidth(OneBasedSpectrumNumber), GetDissociationType(OneBasedSpectrumNumber), GetOneBasedPrecursorScanNumber(OneBasedSpectrumNumber), GetPrecursorMonoisotopicIntensity(OneBasedSpectrumNumber), GetPrecursorMonoisotopicMZ(OneBasedSpectrumNumber));
        }

        private double GetPrecursorMonoisotopicIntensity(int spectrumNumber)
        {
            spectrumNumber--;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam)
            {
                if (cv.accession.Equals(_peakIntensity))
                {
                    return Convert.ToDouble(cv.value);
                }
            }
            return double.NaN;
        }

        private double GetPrecursorMonoisotopicMZ(int spectrumNumber)
        {
            spectrumNumber--;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam)
            {
                if (cv.accession.Equals(_precursorMass))
                {
                    return double.Parse(cv.value);
                }
            }
            throw new ArgumentNullException("Could not determine precursor monoisotopic mass for " + spectrumNumber + 1);
        }

        private double GetTotalIonCurrent(int spectrumNumber)
        {
            spectrumNumber--;
            foreach (Generated.CVParamType cv in _mzMLConnection.run.spectrumList.spectrum[spectrumNumber].cvParam)
            {
                if (cv.accession.Equals(_totalIonCurrent))
                {
                    return double.Parse(cv.value);
                }
            }
            throw new ArgumentNullException("Could not determine total ion current " + spectrumNumber + 1);
        }

        private int GetOneBasedPrecursorScanNumber(int v)
        {
            do
            {
                v--;
            } while (GetOneBasedScan(v).MsnOrder != 1);
            return v;
        }
    }
}