import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  Svg: React.ComponentType<React.ComponentProps<'svg'>>;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'Real Blazor components',
    Svg: require('@site/static/img/undraw_docusaurus_mountain.svg').default,
    description: (
      <>
        Write ordinary <code>.razor</code> components — markup, <code>@code</code>,
        data binding and event handlers — and host them without a browser or a
        WebView.
      </>
    ),
  },
  {
    title: 'Its own CSS engine',
    Svg: require('@site/static/img/undraw_docusaurus_tree.svg').default,
    description: (
      <>
        A purpose-built parser, cascade, and flex/grid layout engine style and
        position every box, with transitions, keyframe animations, gradients and
        filters rendered through SkiaSharp.
      </>
    ),
  },
  {
    title: 'Native window, headless too',
    Svg: require('@site/static/img/undraw_docusaurus_react.svg').default,
    description: (
      <>
        Runs in a real OpenGL window via Silk.NET, or headlessly to a PNG for
        screenshot-based checks — see <code>--screenshot</code>,{' '}
        <code>--verify</code> and <code>--bench</code> in Getting Started.
      </>
    ),
  },
];

function Feature({title, Svg, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <Svg className={styles.featureSvg} role="img" />
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
