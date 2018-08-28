﻿// Copyright 2012, 2013, 2014 Derek J. Bailey
// Modified work copyright 2016 Stefan Solntsev
//
// This file (TestFragments.cs) is part of Proteomics.
//
// Proteomics is free software: you can redistribute it and/or modify it
// under the terms of the GNU Lesser General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Proteomics is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public
// License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with Proteomics. If not, see <http://www.gnu.org/licenses/>.

using Chemistry;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using Proteomics;
using Proteomics.AminoAcidPolymer;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test
{
    [TestFixture]
    public sealed class TestFragments
    {
        private Peptide _mockPeptideEveryAminoAcid;

        [SetUp]
        public void SetUp()
        {
            _mockPeptideEveryAminoAcid = new Peptide("ACDEFGHIKLMNPQRSTVWY");
        }

        [Test]
        public void FragmentNumberToHigh()
        {
            Assert.Throws<IndexOutOfRangeException>(() => _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.b, 25).ToList());
        }

        [Test]
        public void FragmentName()
        {
            Fragment fragment = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.a, 1).ToArray()[0];

            Assert.AreEqual("a1", fragment.ToString());
        }

        [Test]
        public void FragmentAllBIons()
        {
            List<Fragment> fragments = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.b).ToList();

            Assert.AreEqual(19, fragments.Count);
        }

        [Test]
        public void FragmentAnotherTest()
        {
            List<Fragment> fragments = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.b, 1, 2).ToList();

            Assert.AreEqual(2, fragments.Count);
        }

        [Test]
        public void FragmentNTermModTest()
        {
            _mockPeptideEveryAminoAcid.AddModification(new OldSchoolChemicalFormulaModification(ChemicalFormula.ParseFormula("O"), "lala", ModificationSites.NTerminus));
            Fragment fragment = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.b, 1).First();
            Assert.IsTrue(fragment.Modifications.SequenceEqual(new List<OldSchoolModification> { new OldSchoolChemicalFormulaModification(ChemicalFormula.ParseFormula("O"), "lala", ModificationSites.NTerminus) }));
        }

        [Test]
        public void FragmentModifications()
        {
            _mockPeptideEveryAminoAcid.AddModification(new OldSchoolModification(1, "mod1", ModificationSites.C));
            _mockPeptideEveryAminoAcid.AddModification(new OldSchoolModification(2, "mod2", ModificationSites.D));
            _mockPeptideEveryAminoAcid.AddModification(new OldSchoolModification(3, "mod3", ModificationSites.A));
            _mockPeptideEveryAminoAcid.AddModification(new OldSchoolModification(4, "mod4", ModificationSites.Y));
            Fragment fragment = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.b, 1).First();
            Fragment fragmentEnd = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.y, 1).Last();

            Assert.IsTrue(fragment.Modifications.SequenceEqual(new List<OldSchoolModification> { new OldSchoolModification(3, "mod3", ModificationSites.A) }));

            Assert.IsTrue(fragmentEnd.Modifications.SequenceEqual(new List<OldSchoolModification> { new OldSchoolModification(4, "mod4", ModificationSites.Y) }));
        }

        [Test]
        public void ChemicalFormulaFragment()
        {
            var a = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.b, true);
            // Can break in 19 places
            Assert.AreEqual(19, a.Count());
            Assert.IsTrue(a.Select(b => b.Sequence).Contains("ACDEFG"));

            var y = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.y, true);
            // Can break in 19 places
            Assert.IsTrue(y.Select(b => b.Sequence).Contains("TVWY"));

            var c = _mockPeptideEveryAminoAcid.Fragment(FragmentTypes.b, true);

            Assert.AreEqual(a.First(), c.First());
        }

        [Test]
        public void TestGetSiteDeterminingFragments()
        {
            var pep1 = new Peptide("ACDEFG");
            var pep2 = new Peptide("ACTVWY");
            var ok = pep1.GetSiteDeterminingFragments(pep2, FragmentTypes.b);
            Assert.AreEqual(6, ok.Count());
            Assert.Contains("ACT", ok.Select(b => b.Sequence).ToArray());
        }

        [Test]
        public void TestFormulaTerminusMods()
        {
            var pep1 = new Peptide("ACDEFG");
            pep1.AddModification(new OldSchoolChemicalFormulaModification(ChemicalFormula.ParseFormula("H"), ModificationSites.NTerminus));

            Assert.IsTrue(pep1.Fragment(FragmentTypes.b, true).First() is IHasChemicalFormula);

            var pep2 = new Peptide("ACDEFG");
            pep2.AddModification(new OldSchoolModification(2, "haha", ModificationSites.NTerminus));
            Assert.IsFalse(pep2.Fragment(FragmentTypes.b, true).First() is IHasChemicalFormula);

            var pep3 = new Peptide("ACDEFG");
            pep3.AddModification(new OldSchoolModification(3, "haha", ModificationSites.D));

            var list = pep3.Fragment(FragmentTypes.b, true).ToList();

            Assert.IsTrue(list[0] is IHasChemicalFormula);
            Assert.IsFalse(list[2] is IHasChemicalFormula);
        }

        [Test]
        public void CleavageIndicesTest()
        {
            IEnumerable<IProtease> proteases = new List<TestProtease> { new TestProtease() };
            var ok1 = AminoAcidPolymer.GetCleavageIndexes("ACDEFG", proteases, true).ToList();
            var ok2 = AminoAcidPolymer.GetCleavageIndexes("ACDEFG", proteases, false).ToList();
            var ok3 = AminoAcidPolymer.GetCleavageIndexes("ACDE", proteases, true).ToList();
            var ok4 = AminoAcidPolymer.GetCleavageIndexes("ACDE", proteases, false).ToList();
            Assert.AreEqual(3, ok1.Count());
            Assert.AreEqual(2, ok2.Count());
            Assert.AreEqual(4, ok3.Count());
            Assert.AreEqual(2, ok4.Count());
        }

        [Test]
        public void TestGetIonCapFailFail()
        {
            FragmentTypes f = FragmentTypes.All;
            Assert.That(() => f.GetIonCap(), Throws.TypeOf<MzLibException>()
                                            .With.Property("Message")
                                            .EqualTo("Fragment Type must be a single value to determine the ion cap"));
        }

        [Test]
        public void TestGetTerminusFail()
        {
            FragmentTypes f = FragmentTypes.a | FragmentTypes.adot;
            Assert.That(() => f.GetTerminus(), Throws.TypeOf<MzLibException>()
                                            .With.Property("Message")
                                            .EqualTo("Fragment Type must be a single value to determine the terminus"));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_UnmodifiedPeptide()
        {
            Protein p = new Protein("PET", "accession");
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification>(), new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);

            //evaluate N-terminal masses
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 97, 226 };
            Assert.That(expectedNTerminalMasses.SetEquals(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));

            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();

            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;226.095356938-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 119, 248 };
            Assert.That(expectedCTerminalMasses.SetEquals(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;119.058243153-0", "Y2;248.100836242-0" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_nTerminalModifiedPeptide()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"));
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);

            //evaluate N-terminal masses
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 177, 306 };
            Assert.That(expectedNTerminalMasses.SetEquals(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;177.019094739-0", "B2;306.061687827-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 119, 248 };
            Assert.That(expectedCTerminalMasses.SetEquals(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;119.058243153-0", "Y2;248.100836242-0" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_cTerminalModifiedPeptide()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("T", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"));
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);

            //evaluate N-terminal masses
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 97, 226 };
            Assert.That(expectedNTerminalMasses.SetEquals(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;226.095356938-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 199, 328 };
            Assert.That(expectedCTerminalMasses.SetEquals(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;199.024574042-0", "Y2;328.067167131-0" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_internallyModifiedPeptide()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("E", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"));
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);

            //evaluate N-terminal masses
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 97, 306 };
            HashSet<int> foundNTerminalMasses = new HashSet<int>(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0)).ToList());

            Assert.That(expectedNTerminalMasses.SetEquals(foundNTerminalMasses));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;306.061687827-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 119, 328 };
            HashSet<int> foundCTerminalMasses = new HashSet<int>(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0)).ToList());

            Assert.That(expectedCTerminalMasses.SetEquals(foundCTerminalMasses));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;119.058243153-0", "Y2;328.067167131-0" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_nTerminalModifiedPeptide_NeutralLoss()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.HCD, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);

            //evaluate N-terminal masses
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 177, 306, 79, 208 };
            Assert.That(expectedNTerminalMasses.SetEquals(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;177.019094739-0", "B2;306.061687827-0", "B1;79.04219916561-97.97689557339", "B2;208.08479225361-97.97689557339" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 119, 248 };
            Assert.That(expectedCTerminalMasses.SetEquals(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;119.058243153-0", "Y2;248.100836242-0" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_cTerminalModifiedPeptide_NeutralLoss()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("T", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.HCD, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);

            //var nTerminalMasses = aCompactPeptide.TerminalMasses.Where(v => v.Terminus == FragmentationTerminus.N);
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> foundNTerminalMasses = new HashSet<int>(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0)).ToList());
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 97, 226 };

            Assert.That(expectedNTerminalMasses.SetEquals(foundNTerminalMasses));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;226.095356938-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> foundCTerminalMasses = new HashSet<int>(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0)).ToList());
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 199, 328, 101, 230 };

            Assert.That(expectedCTerminalMasses.SetEquals(foundCTerminalMasses));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;199.024574042-0", "Y2;328.067167131-0", "Y1;101.04767846861-97.97689557339", "Y2;230.09027155761-97.97689557339" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_internallyModifiedPeptide_NeutralLoss()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("E", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.HCD, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);

            //evaluate N-terminal masses
            var n = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 97, 306, 208 };
            Assert.That(expectedNTerminalMasses.SetEquals(n.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;306.061687827-0", "B2;208.08479225361-97.97689557339" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var c = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 119, 328, 230 };
            Assert.That(expectedCTerminalMasses.SetEquals(c.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;119.058243153-0", "Y2;328.067167131-0", "Y2;230.09027155761-97.97689557339" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_nTerminalModifiedPeptide_NeutralLoss_DissociationTypes_AnyActivationType_and_HCD()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.AnyActivationType, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.AnyActivationType, FragmentationTerminus.Both);

            //evaluate N-terminal masses
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 177, 306, 79, 208 };
            Assert.That(expectedNTerminalMasses.SetEquals(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;177.019094739-0", "B2;306.061687827-0", "B1;79.04219916561-97.97689557339", "B2;208.08479225361-97.97689557339" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 119, 248 };
            Assert.That(expectedCTerminalMasses.SetEquals(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;119.058243153-0", "Y2;248.100836242-0" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_nTerminalModifiedPeptide_NeutralLoss_DissociationTypes_CID_and_HCD()//there should be no added neutral losses in this case becuase the allowed dissociation type doesn't match the dissociation type used in the experiment
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.CID, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);//Note that dissociation type here intentionally mismatched to dissociation type in modification constructor

            //evaluate N-terminal masses
            var nTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).ToList();
            HashSet<int> expectedNTerminalMasses = new HashSet<int> { 177, 306 };
            Assert.That(expectedNTerminalMasses.SetEquals(nTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;177.019094739-0", "B2;306.061687827-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            //evaluate C-terminal masses
            var cTerminalMasses = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).ToList();
            HashSet<int> expectedCTerminalMasses = new HashSet<int> { 119, 248 };
            Assert.That(expectedCTerminalMasses.SetEquals(cTerminalMasses.Select(v => (int)Math.Round(v.NeutralMass, 0))));
            var cTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.C).Select(f => f.ToString()).ToList();
            HashSet<string> expectedCTerminalMassesLabels = new HashSet<string> { "Y1;119.058243153-0", "Y2;248.100836242-0" };
            Assert.That(expectedCTerminalMassesLabels.SetEquals(cTerminalMassesLabels));
        }

        [Test]
        public static void Test_GetTheoreticalFragments_ProductTypeLabel()
        {
            Protein p = new Protein("PET", "accession");
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification>(), new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.N);
            var nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            HashSet<string> expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;226.095356938-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.AnyActivationType, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;226.095356938-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.CID, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;226.095356938-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.ECD, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { "C1;114.079312951-0", "C2;243.121906039-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.ETD, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { "C1;114.079312951-0", "C2;243.121906039-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.EThCD, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { "B1;97.05276385-0", "B2;226.095356938-0", "C1;114.079312951-0", "C2;243.121906039-0" };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.ISCID, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            DissociationTypeCollection.ProductsFromDissociationType[DissociationType.Custom] = new List<ProductType> { };
            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.Custom, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.MPD, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));

            theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.PQD, FragmentationTerminus.N);
            nTerminalMassesLabels = theseTheoreticalFragments.Where(f => f.TerminusFragment.Terminus == FragmentationTerminus.N).Select(f => f.ToString()).ToList();
            expectedNTerminalMassesLabels = new HashSet<string> { };
            Assert.That(expectedNTerminalMassesLabels.SetEquals(nTerminalMassesLabels));
        }

        [Test]
        public static void Test_Fragment_DiagnosticIons()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.HCD, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } }, _diagnosticIons: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.HCD, new List<double> { ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);//Note that dissociation type here intentionally mismatched to dissociation type in modification constructor

            //evaluate N-terminal masses
            var diagnosticIons = theseTheoreticalFragments.Where(f => f.ProductType == ProductType.D).ToList();
            Assert.AreEqual("D0;97.976895573-0", diagnosticIons.First().ToString());
        }

        [Test]
        public static void Test_Fragment_MolecularIon_NeutralLoss()
        {
            Protein p = new Protein("PTE", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.HCD, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } }, _diagnosticIons: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.HCD, new List<double> { ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);//Note that dissociation type here intentionally mismatched to dissociation type in modification constructor

            //evaluate N-terminal masses
            var molecularIons = theseTheoreticalFragments.Where(f => f.ProductType == ProductType.M).ToList();
            Assert.AreEqual("M0;327.14303540761-97.97689557339", molecularIons.First().ToString());
        }

        [Test]
        public static void Test_Fragment_DiagnosticIons_unmatchedDissociationType()
        {
            Protein p = new Protein("PET", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.CID, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } }, _diagnosticIons: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.CID, new List<double> { ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);//Note that dissociation type here intentionally mismatched to dissociation type in modification constructor

            //evaluate N-terminal masses
            var diagnosticIons = theseTheoreticalFragments.Where(f => f.ProductType == ProductType.D).ToList();
            Assert.AreEqual(0, diagnosticIons.Count());
        }

        [Test]
        public static void Test_Fragment_MolecularIon_NeutralLoss_unmatchedDissociationType()
        {
            Protein p = new Protein("PTE", "accession");
            ModificationMotif.TryGetMotif("P", out ModificationMotif motif);
            Modification phosphorylation = new Modification(_id: "phospho", _modificationType: "CommonBiological", _target: motif, _locationRestriction: "Anywhere.", _chemicalFormula: ChemicalFormula.ParseFormula("H1O3P1"), _neutralLosses: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.CID, new List<double> { 0, ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } }, _diagnosticIons: new Dictionary<DissociationType, List<double>> { { MassSpectrometry.DissociationType.CID, new List<double> { ChemicalFormula.ParseFormula("H3O4P1").MonoisotopicMass } } });
            DigestionParams digestionParams = new DigestionParams(minPeptideLength: 2);
            var aPeptideWithSetModifications = p.Digest(digestionParams, new List<Modification> { phosphorylation }, new List<Modification>()).First();

            var theseTheoreticalFragments = aPeptideWithSetModifications.Fragment(DissociationType.HCD, FragmentationTerminus.Both);//Note that dissociation type here intentionally mismatched to dissociation type in modification constructor

            //evaluate N-terminal masses
            var molecularIons = theseTheoreticalFragments.Where(f => f.ProductType == ProductType.M).ToList();
            Assert.AreEqual(0, molecularIons.Count());
        }

        [Test]
        public static void Test_NeutralMassShiftFromProductType()
        {
            foreach (ProductType p in Enum.GetValues(typeof(ProductType)))
            {
                double mass = Chemistry.ClassExtensions.RoundedDouble(DissociationTypeCollection.ProductTypeSpecificFragmentNeutralMass(0, p)).Value;
                switch (p)
                {
                    case ProductType.A:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("C-1O-1").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.Adot:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("C-1O-2H-2").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.Astar:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("C-1O-1N-1H-3").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.B:
                        Assert.AreEqual(0, mass);
                        break;

                    case ProductType.Bdot:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("H-2O-1").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.Bstar:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("N-1H-3").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.C:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("N1H3").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.D:
                        Assert.AreEqual(0, mass);
                        break;

                    case ProductType.M:
                        Assert.AreEqual(0, mass);
                        break;

                    case ProductType.X:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("C1O2").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.Y:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("H2O1").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.Ydot:
                        Assert.AreEqual(0, mass);
                        break;

                    case ProductType.Ystar:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("O1H-1N-1").MonoisotopicMass).Value, mass);
                        break;

                    case ProductType.Zdot:
                        Assert.AreEqual(Chemistry.ClassExtensions.RoundedDouble(ChemicalFormula.ParseFormula("O1H1N-1").MonoisotopicMass).Value, mass);
                        break;
                }
            }
        }

        [Test]
        public static void Test_NeutralMassShiftFromProductType_Exceptions()
        {
            ProductType undefinedProduct = (ProductType)99;

            Assert.Throws<MzLibException>(() => DissociationTypeCollection.ProductTypeSpecificFragmentNeutralMass(0, undefinedProduct), "Unknown product type!");
        }

        [Test]
        public static void Test_CustomDissociationType()
        {
            DissociationTypeCollection.ProductsFromDissociationType[DissociationType.Custom].Add(ProductType.B);
            DissociationTypeCollection.ProductsFromDissociationType[DissociationType.Custom].Add(ProductType.Y);
            Assert.IsTrue(DissociationTypeCollection.ProductsFromDissociationType[DissociationType.Custom].Contains(ProductType.B));

            var productCollection = TerminusSpecificProductTypes.ProductIonTypesFromSpecifiedTerminus[FragmentationTerminus.N].Intersect(DissociationTypeCollection.ProductsFromDissociationType[DissociationType.Custom]);
            Assert.IsTrue(productCollection.Contains(ProductType.B));

            productCollection = TerminusSpecificProductTypes.ProductIonTypesFromSpecifiedTerminus[FragmentationTerminus.C].Intersect(DissociationTypeCollection.ProductsFromDissociationType[DissociationType.Custom]);
            Assert.IsTrue(productCollection.Contains(ProductType.Y));
        }
    }
}