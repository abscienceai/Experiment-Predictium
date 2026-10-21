# Water-Stability Data Framework

## Purpose

The initial water-stability pilot is broadened from a single keyword into a controlled literature/data framework, following WG4 feedback.

## Controlled search vocabulary

Core terms include:

- water stability
- aqueous stability
- hydrolytic stability
- moisture stability
- humidity stability
- water resistance
- hydrolysis
- structural integrity after water exposure
- stability in aqueous media
- stability in physiological media

The vocabulary should be expanded with spelling variants and material-specific terminology when screening literature.

## Minimum record fields

- MOF identifier/name
- DOI / source
- metal node
- linker
- topology, when available
- particle/crystal size, when relevant
- exposure medium
- pH
- temperature
- exposure duration
- concentration / solid-to-liquid ratio, when reported
- stability endpoint
- characterization method (PXRD/XRD, BET, spectroscopy, microscopy, dissolution/ICP, etc.)
- qualitative or quantitative outcome
- evidence text / table / figure locator
- curator note

## Evidence categories

1. **Structural retention**: crystallinity or framework integrity after exposure.
2. **Porosity retention**: BET/pore-volume retention after exposure.
3. **Chemical integrity**: linker/metal release or decomposition evidence.
4. **Colloidal/particle behavior**: aggregation or morphology changes.
5. **Application-conditioned stability**: stability under the actual use medium and conditions.

A binary `stable / unstable` label should only be created after explicit inclusion rules are defined. Source conditions must remain attached to every label to avoid merging incompatible experiments.
