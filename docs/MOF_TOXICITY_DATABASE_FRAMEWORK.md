# MOF Nanoparticle Toxicity Database Framework

## Status

WG4-aligned follow-up/extension inspired by discussion with EU4MOFs leadership and by the tutorial review:

R. Ettlinger et al., *Toxicity of metal-organic framework nanoparticles: from essential analyses to potential applications*, Chemical Society Reviews 51 (2022) 464-484. DOI: 10.1039/D1CS00918D.

This framework is a structured data foundation. It does **not** claim that a validated general-purpose MOF toxicity predictor already exists.

## Core schema

### Provenance
- record_id
- DOI
- citation
- publication_year
- table/figure/page/source locator
- curator

### MOF identity
- MOF name / identifier
- metal node / oxidation state when available
- linker
- topology
- composition
- synthesis route

### Physical identity
- particle size and distribution
- morphology/shape
- aggregation/agglomeration state
- porosity/surface area
- zeta potential / surface charge
- chemical stability
- colloidal stability

### Synthetic identity
- surface modification/coating
- coating chemistry
- coating density or amount, when available
- loaded cargo/drug, if any

### Biological identity / exposure
- biological medium
- protein-corona information, when available
- route of exposure/administration
- in vitro / ex vivo / in vivo
- cell line / primary cells / organism
- healthy vs disease model
- dose/concentration
- exposure time

### Assay and endpoint
- assay type (MTT, CCK-8, MTS, XTT, LDH, ROS, etc.)
- endpoint
- cell viability
- IC50, when reported
- membrane-damage endpoint
- oxidative-stress endpoint
- inflammatory/immune endpoint
- biodistribution/organ endpoint for in vivo data

### Quality / comparability
- control reported
- replicate information
- particle characterization completeness
- exposure-condition completeness
- assay-interference concern
- units standardized
- metadata completeness class

## Analysis path

1. literature retrieval and provenance capture;
2. harmonization without discarding experimental context;
3. metadata completeness assessment;
4. descriptive analysis of MOFs, cells, doses, assays and exposure times;
5. only if data quantity/quality supports it: ML benchmarking, uncertainty and OOD analysis;
6. comparison with multi-laboratory round-robin results using the implemented local `MOF-NP Round Robin` module when real study data become available.

## Round-robin relevance

The database can inform candidate/material selection and reveal which dose ranges, cell models, exposure times and assays are sufficiently common to support a reproducibility study. Experiment-Predictium now includes a local template/merge/reproducibility-analysis module to support this workflow. Experimental protocol decisions and biological interpretation remain with the toxicology/biomedical experts leading the round-robin study.
