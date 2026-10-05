import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  image: string;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'Flutter-style widgets in C#',
    image: '/img/feature-widgets.svg',
    description: (
      <>
        Immutable widgets, stateful <code>State</code> with <code>SetState</code>, inherited
        widgets and box-constraint layout: the Flutter model, written in plain C#
        with no browser, WebView or XAML.
      </>
    ),
  },
  {
    title: 'Material 3 by default',
    image: '/img/feature-material.svg',
    description: (
      <>
        Colour schemes from a seed, the full type scale, elevation, light and dark
        modes, floating-label text fields, buttons, navigation and dialogs, with
        right-to-left layouts and animation built in.
      </>
    ),
  },
  {
    title: 'Native window, headless too',
    image: '/img/feature-native.svg',
    description: (
      <>
        Renders with SkiaSharp in a real OpenGL window via Silk.NET, or headlessly to
        a PNG for screenshot-based checks, with a scriptable clock for deterministic
        animation frames.
      </>
    ),
  },
];

function Feature({title, image, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <img className={styles.featureSvg} src={image} alt="" />
      </div>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
