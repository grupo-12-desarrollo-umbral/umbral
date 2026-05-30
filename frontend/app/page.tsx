import Image from 'next/image';
import Link from 'next/link';
import styles from './page.module.css';

export default function HomePage() {
  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link href="/" className={styles.brand}>
          <span className={styles.brandMark} aria-hidden="true">
            <CompassMark />
          </span>
          <span className={styles.brandTitle}>Umbral</span>
        </Link>
        <nav className={styles.nav}>
          <Link href="/dashboard" className={styles.navLink}>
            Centro de Mando
          </Link>
        </nav>
      </header>

      <main>
        <section className={styles.hero} aria-label="Bienvenido a Umbral">
          <div className={styles.heroImageWrap}>
            <Image
              src="/img/hero-background.png"
              alt="Dos aventureros en un sendero de piedra contemplan un valle dorado al atardecer"
              fill
              priority
              className={styles.heroImage}
              sizes="100vw"
            />
            <div className={styles.heroOverlay} aria-hidden="true" />
          </div>

          <div className={styles.heroContent}>
            <span className={styles.eyebrow}>Juegos en vivo en el mundo real</span>
            <h1 className={styles.heroTitle}>Tu próxima aventura empieza aquí.</h1>
            <p className={styles.heroSubtitle}>
              Diseña cacerías del tesoro, trivia y misiones que se juegan en calles reales. 
              Desde la primera pista hasta la tabla de clasificación final: todo bajo control, 
              todo en tiempo real.
            </p>
            <div className={styles.heroActions}>
              <Link href="/dashboard" className={styles.primaryButton}>
                Abrir el Centro de Mando
              </Link>
              <a href="#about" className={styles.secondaryButton}>
                Descubre cómo funciona
              </a>
            </div>
          </div>
        </section>

        <section id="about" className={styles.section} aria-labelledby="about-title">
          <div className={styles.container}>
            <h2 id="about-title" className={styles.sectionTitle}>
              Un maestro de juegos en el que confiar
            </h2>
            <p className={styles.sectionBody}>
              En el fragor de una partida en vivo, cada segundo cuenta. Umbral da a los operadores
              la confianza para dirigir sesiones complejas en tiempo real — y a los jugadores un
              punto de entrada que se siente como el comienzo de algo inolvidable.
            </p>

            <div className={styles.cards}>
              <article className={styles.card}>
                <h3 className={styles.cardTitle}>Para operadores</h3>
                <p className={styles.cardBody}>
                  Diseña misiones, libera pistas y observa tablas de clasificación en vivo. Denso,
                  responsivo y siempre bajo control.
                </p>
              </article>
              <article className={styles.card}>
                <h3 className={styles.cardTitle}>Para jugadores</h3>
                <p className={styles.cardBody}>
                  Únete a un equipo, sigue las pistas y corre hacia la meta. La ciudad es tu campo
                  de juego.
                </p>
              </article>
              <article className={styles.card}>
                <h3 className={styles.cardTitle}>Magia en tiempo real</h3>
                <p className={styles.cardBody}>
                  Actualizaciones impulsadas por SignalR, puntos de control con QR y validación
                  instantánea. El juego sigue vivo desde la primera pista hasta la puntuación final.
                </p>
              </article>
            </div>
          </div>
        </section>
      </main>

      <footer className={styles.footer}>
        <div className={styles.footerBrand}>
          <span className={styles.brandMark} aria-hidden="true">
            <CompassMark />
          </span>
          <span className={styles.brandTitle}>Umbral</span>
        </div>
        <p className={styles.footerCopy}>Construido para quienes exploran, diseñan y ganan.</p>
      </footer>
    </div>
  );
}

function CompassMark() {
  return (
    <svg aria-hidden="true" height="34" viewBox="0 0 34 34" width="34">
      <circle cx="17" cy="17" fill="none" r="15.5" stroke="currentColor" strokeWidth="1.2" />
      <circle cx="17" cy="17" fill="none" opacity="0.35" r="10.4" stroke="currentColor" strokeWidth="1" />
      <path
        d="M17 4.5 19.8 14.2 29.5 17 19.8 19.8 17 29.5 14.2 19.8 4.5 17 14.2 14.2Z"
        fill="none"
        stroke="currentColor"
        strokeLinejoin="round"
        strokeWidth="1.15"
      />
      <path d="M17 7.5V26.5M7.5 17H26.5" opacity="0.42" stroke="currentColor" strokeLinecap="round" strokeWidth="1" />
    </svg>
  );
}
